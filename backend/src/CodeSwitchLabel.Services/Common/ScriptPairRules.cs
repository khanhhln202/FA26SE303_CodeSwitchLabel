using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Common;

/// <summary>
/// TEAM_015: luật của một CẶP CÂU (nội dung + lời giải Anh–Việt) tách khỏi ScriptService
/// để hai luồng cùng dùng mà không chép luật: duyệt nội dung một lượt, và xử lý đề xuất
/// câu của Reviewer — lượt duyệt thứ hai cũng ghi thẳng vào cặp câu. Chép luật ra hai chỗ
/// thì sớm muộn hai đường ghi cũng chấp nhận hai bộ nội dung khác nhau.
///
/// Đây là code chuyển nguyên vẹn từ ScriptService, không đổi hành vi.
/// </summary>
internal static class ScriptPairRules
{
    /// <summary>
    /// Kiểm tra một cặp câu. Trả về tổng số từ của câu chen tiếng Anh và số từ tiếng Anh trong đó.
    /// </summary>
    public static (int WordCount, int EnWordCount) ValidatePair(string cs, string ve)
    {
        int wordCount, enWordCount, veEnglish;

        try
        {
            wordCount = CodeSwitchText.CountWords(cs);
            enWordCount = CodeSwitchText.CountEnglishWords(cs);
            veEnglish = CodeSwitchText.CountEnglishWords(ve);
        }
        catch (FormatException ex)
        {
            throw new UnprocessableException("invalid_language_tags", $"Nhãn ngôn ngữ sai: {ex.Message}");
        }

        // Chữ số đầu của script_id chính là số từ tiếng Anh, nên chỉ chứa được một chữ số.
        if (enWordCount is < 1 or > 9)
        {
            throw new UnprocessableException(
                "en_word_count_out_of_range",
                $"Câu phải có từ 1 đến 9 từ tiếng Anh, hiện có {enWordCount}. " +
                "Con số này nằm trong mã script nên không thể lớn hơn một chữ số.");
        }

        if (veEnglish > 0)
        {
            throw new UnprocessableException(
                "ve_not_pure_vietnamese",
                $"Câu tương đương phải thuần Việt, nhưng đang có {veEnglish} từ gắn nhãn [en].");
        }

        return (wordCount, enWordCount);
    }

    /// <summary>Có một từ là danh từ riêng thì cả câu mang mã quan hệ 2 (ProperNoun).</summary>
    public static ScriptRelation RelationOf(List<ScriptWord> words) =>
        words.Any(w => w.Relation == ScriptWordRelation.ProperNoun)
            ? ScriptRelation.ProperNoun
            : ScriptRelation.DirectTranslation;

    /// <summary>
    /// Quan hệ ghi trong mã câu: chữ số thứ ba (vị trí 5) là 2 khi câu có danh từ riêng.
    /// </summary>
    public static ScriptRelation RelationDigitOf(string scriptId) =>
        scriptId.Length >= 5 && scriptId[4] == '2'
            ? ScriptRelation.ProperNoun
            : ScriptRelation.DirectTranslation;

    /// <summary>
    /// Đổi alignment thành các dòng script_word — đúng một dòng cho mỗi từ tiếng Anh.
    /// Thiếu hay thừa dòng đều bị từ chối vì trigger defer của database buộc số dòng khớp en_word_count.
    /// </summary>
    public static List<ScriptWord> BuildWords(List<AlignmentItem> alignment, int enWordCount)
    {
        if (alignment.Count != enWordCount)
        {
            throw new UnprocessableException(
                "alignment_incomplete",
                $"Câu có {enWordCount} từ tiếng Anh nhưng alignment có {alignment.Count} mục. " +
                "Mỗi từ tiếng Anh phải có đúng một mục alignment từ [en] sang nghĩa tiếng Việt.");
        }

        var words = new List<ScriptWord>(alignment.Count);

        for (var i = 0; i < alignment.Count; i++)
        {
            var item = alignment[i];
            var en = (item.Source ?? string.Empty).Trim();

            if (en.Length == 0)
            {
                throw new UnprocessableException(
                    "alignment_word_missing", $"Mục alignment thứ {i + 1} thiếu từ tiếng Anh.");
            }

            var isProper = (item.Relation ?? string.Empty).Contains("proper", StringComparison.OrdinalIgnoreCase);

            // Danh từ riêng giữ nguyên, không dịch: lược đồ bắt buộc vi_word = en_word.
            var vi = isProper ? en : (item.Target ?? string.Empty).Trim();

            if (vi.Length == 0)
            {
                throw new UnprocessableException(
                    "alignment_word_missing", $"Mục alignment thứ {i + 1} thiếu nghĩa tiếng Việt.");
            }

            words.Add(new ScriptWord
            {
                WordPosition = (short)(i + 1),
                EnWord = en,
                ViWord = vi,
                Relation = isProper ? ScriptWordRelation.ProperNoun : ScriptWordRelation.SemanticEquivalent
            });
        }

        return words;
    }

    /// <summary>Đổi các dòng script_word thành alignment trả cho client, đúng thứ tự vị trí.</summary>
    public static IReadOnlyList<AlignmentItem> ToAlignment(IEnumerable<ScriptWord> words) =>
        [.. words
            .OrderBy(w => w.WordPosition)
            .Select(w => new AlignmentItem
            {
                Source = w.EnWord,
                Target = w.ViWord,
                Relation = SnakeCaseNaming.ToSnakeCase(w.Relation.ToString())
            })];

    /// <summary>Dữ liệu cũ hoặc dữ liệu ghi thẳng bằng SQL có thể thiếu nhãn — không vì thế mà vỡ API.</summary>
    public static string SafeStrip(string content)
    {
        try { return CodeSwitchText.Strip(content); }
        catch (FormatException) { return content; }
    }

    /// <summary>
    /// Kiểm tra bản sửa trước khi lượt duyệt được ghi, và tính sẵn những thứ
    /// <see cref="ApplyEdit"/> cần để không phải đếm lại lần nữa.
    /// Tách khỏi ApplyEdit vì cả hai luồng (duyệt nội dung, xử lý đề xuất) phải chạy hết
    /// kiểm tra này TRƯỚC khi ghi — nếu không, một bản sửa sai sẽ để lại lượt duyệt mồ côi.
    /// </summary>
    public static async Task<EditPlan> ValidateEditContentAsync(
        IScriptRepository repository, Script script, string cs, string ve,
        List<AlignmentItem>? alignment, CancellationToken ct)
    {
        if (await repository.HasRecordingsAsync(script.ScriptId, ct))
        {
            throw new ConflictException(
                "script_already_recorded",
                $"Cặp câu {script.ScriptId} đã có bản ghi âm nên không sửa nội dung được. " +
                "Sửa sẽ làm transcript của bản ghi cũ không còn khớp với âm thanh.");
        }

        var (wordCount, enWordCount) = ValidatePair(cs, ve);

        if (enWordCount != script.EnWordCount)
        {
            throw new UnprocessableException(
                "en_word_count_locked",
                $"Bản sửa có {enWordCount} từ tiếng Anh, khác {script.EnWordCount} từ ghi trong mã {script.ScriptId}. " +
                "Muốn đổi số từ tiếng Anh thì phải tạo cặp câu mới.");
        }

        // Sửa cả alignment thì thay cả bộ dòng script_word của câu. Số từ tiếng Anh phải giữ nguyên,
        // và quan hệ Anh–Việt cũng không đổi được vì nó là chữ số trong mã câu.
        List<ScriptWord>? words = null;

        if (alignment is { Count: > 0 } edited)
        {
            words = BuildWords(edited, enWordCount);

            if (RelationOf(words) != RelationDigitOf(script.ScriptId))
            {
                throw new UnprocessableException(
                    "relation_locked",
                    $"Bản sửa đổi quan hệ Anh–Việt, nhưng quan hệ đã nằm trong mã {script.ScriptId} nên không đổi được.");
            }
        }

        return new EditPlan(wordCount, words);
    }

    /// <summary>
    /// Ghi nội dung mới đè lên cặp câu. Lược đồ không lưu phiên bản cũ, nên chỉ cho sửa khi
    /// chưa có bản ghi âm nào. Số từ tiếng Anh cũng không được đổi: nó nằm trong mã script.
    /// </summary>
    public static void ApplyEdit(IScriptRepository repository, Script script, string cs, string ve, EditPlan plan)
    {
        if (plan.Words is not null)
        {
            repository.RemoveWords(script.Words);
            foreach (var word in plan.Words) word.ScriptId = script.ScriptId;
            repository.AddWords(plan.Words);
        }

        script.CsContent = cs;
        script.ViContent = ve;
        script.WordCount = plan.WordCount;
    }
}

/// <summary>Kết quả kiểm tra bản sửa, tính một lần rồi đưa sang <see cref="ScriptPairRules.ApplyEdit"/> dùng lại.</summary>
internal record EditPlan(int WordCount, List<ScriptWord>? Words);
