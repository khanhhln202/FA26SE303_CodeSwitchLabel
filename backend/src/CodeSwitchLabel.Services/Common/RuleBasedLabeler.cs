using System.Text;
using System.Text.RegularExpressions;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Common;

/// <summary>
/// Gán nhãn [vi]/[en] bằng luật, không gọi LLM. Từ có ký tự ngoài ASCII là tiếng Việt;
/// từ ASCII không nằm trong danh sách âm tiết Việt không dấu là tiếng Anh.
///
/// Nghĩa tiếng Việt lấy bằng cách đối chiếu hai câu: từ giống nhau ở cả hai câu là mốc,
/// đoạn chữ Việt giữa hai mốc là nghĩa của đoạn tiếng Anh giữa hai mốc đó. Nhờ vậy một
/// từ tiếng Anh có thể ứng với nhiều từ tiếng Việt (email → "thư điện tử") mà không làm
/// lệch các từ phía sau.
/// </summary>
internal static class RuleBasedLabeler
{
    private static readonly Regex TokenPattern = new(@"\S+", RegexOptions.Compiled);

    // shortcut: mốc là từ trùng nguyên văn ở cả hai câu; đoạn không có mốc thì chia theo
    // tỉ lệ vị trí. Câu Việt đổi cả từ xung quanh từ tiếng Anh vẫn có thể lệch — nâng cấp
    // bằng từ điển/căn chỉnh thật nếu cần độ chính xác cao hơn.

    // shortcut: chỉ nhận ra tiếng Việt không dấu nhờ danh sách ngắn này; văn bản không dấu
    // dài sẽ bị gắn [en]. Nâng cấp bằng từ điển âm tiết nếu người dùng thật sự gõ không dấu.
    private static readonly HashSet<string> VietnameseStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "la", "cho", "co", "hay", "ho", "ma", "voi", "nay", "ngay", "hai", "tu", "khac",
        "loi", "truy", "goc", "gop", "truoc", "sau", "duong", "dan", "hop", "giao", "dien",
        "dung", "dang", "phien", "ban", "chua", "cai", "dat", "phan", "mem", "chia", "se",
        "man", "hinh", "nen", "em", "anh", "chi", "toi", "minh", "cua", "trong", "duoc",
        "khong", "mot", "nhu", "thi", "ra", "vao", "len", "xuong", "di", "den", "roi",
        "cung", "rat", "tai", "lieu", "nao", "sao", "gi", "nua", "kia", "qua", "khi", "tan", "nha"
    };

    /// <summary>
    /// Trả câu đã chèn nhãn và một alignment cho mỗi từ tiếng Anh. Nghĩa tiếng Việt là đoạn
    /// giữa hai từ chung của hai câu; hết chữ Việt tương ứng thì nghĩa để trống.
    /// </summary>
    public static LabeledPair Label(string csRaw, string viRaw)
    {
        var tagged = Tag(csRaw);
        var englishWords = EnglishWords(tagged);
        var csWords = CodeSwitchText.Tokenize(csRaw);
        var viWords = CodeSwitchText.Tokenize(viRaw);

        // LCS tốn O(n*m) bộ nhớ; câu dài bất thường thì bỏ mốc và lùi về cách lấy theo vị trí.
        var anchors = (long)csWords.Length * viWords.Length > 1_000_000
            ? new List<(int Cs, int Vi)>()
            : LongestCommonSubsequence(csWords, viWords);

        var anchorVi = anchors.ToDictionary(a => a.Cs, a => a.Vi);
        var alignment = new List<AlignmentItem>(englishWords.Count);

        foreach (var (index, word) in englishWords)
        {
            alignment.Add(new AlignmentItem
            {
                Source = word,
                Target = TargetFor(index, csWords.Length, viWords, anchors, anchorVi)
            });
        }

        return new LabeledPair(tagged, alignment);
    }

    /// <summary>Các từ [en] kèm vị trí của chúng trong câu đã bỏ nhãn.</summary>
    private static List<(int Index, string Word)> EnglishWords(string tagged)
    {
        var words = new List<(int, string)>();
        var index = 0;

        foreach (var span in ParseTagged(tagged))
        {
            foreach (var word in CodeSwitchText.Tokenize(span.Text))
            {
                if (span.IsEnglish) words.Add((index, word));
                index++;
            }
        }

        return words;
    }

    /// <summary>Mốc căn chỉnh: các cặp từ giống nhau ở cả hai câu, giữ đúng thứ tự.</summary>
    private static List<(int Cs, int Vi)> LongestCommonSubsequence(string[] cs, string[] vi)
    {
        var lengths = new int[cs.Length + 1, vi.Length + 1];

        for (var i = cs.Length - 1; i >= 0; i--)
        {
            for (var j = vi.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = cs[i] == vi[j]
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var anchors = new List<(int, int)>();

        for (int i = 0, j = 0; i < cs.Length && j < vi.Length;)
        {
            if (cs[i] == vi[j])
            {
                anchors.Add((i, j));
                i++;
                j++;
            }
            else if (lengths[i + 1, j] >= lengths[i, j + 1]) i++;
            else j++;
        }

        return anchors;
    }

    /// <summary>
    /// Nghĩa của từ [en] ở vị trí <paramref name="csIndex"/>: trùng mốc thì lấy đúng từ đó;
    /// nằm giữa hai mốc thì lấy đoạn chữ Việt giữa hai mốc, chia theo tỉ lệ khi đoạn có nhiều
    /// từ tiếng Anh. Hết chữ Việt tương ứng thì trả chuỗi rỗng.
    /// </summary>
    private static string TargetFor(
        int csIndex, int csCount, string[] viWords,
        List<(int Cs, int Vi)> anchors, Dictionary<int, int> anchorVi)
    {
        if (anchorVi.TryGetValue(csIndex, out var viIndex)) return viWords[viIndex];

        // Hai câu không có từ chung thì không có mốc; giữ cách lấy theo vị trí cũ.
        if (anchors.Count == 0) return csIndex < viWords.Length ? viWords[csIndex] : string.Empty;

        var prevCs = -1;
        var prevVi = -1;
        var nextCs = csCount;
        var nextVi = viWords.Length;

        foreach (var (anchorCs, anchorPosition) in anchors)
        {
            if (anchorCs < csIndex)
            {
                prevCs = anchorCs;
                prevVi = anchorPosition;
            }
            else
            {
                nextCs = anchorCs;
                nextVi = anchorPosition;
                break;
            }
        }

        var csGapStart = prevCs + 1;
        var viGapStart = prevVi + 1;
        var csGapCount = nextCs - csGapStart;
        var viGapCount = nextVi - viGapStart;
        var local = csIndex - csGapStart;
        var start = viGapStart + (int)Math.Round(
            local * (double)viGapCount / csGapCount, MidpointRounding.AwayFromZero);
        var end = viGapStart + (int)Math.Round(
            (local + 1) * (double)viGapCount / csGapCount, MidpointRounding.AwayFromZero);

        if (end <= start) return string.Empty;

        var target = string.Join(' ', viWords[start..end]);

        // shortcut: vi_word tối đa 100 ký tự; đoạn quá dài thì để trống thay vì làm hỏng lần nhập.
        return target.Length <= 100 ? target : string.Empty;
    }

    // Câu thô có thể chứa "[" (Parse coi là nhãn lạ) hoặc chỉ toàn dấu câu; đổi lỗi định
    // dạng thành lỗi nghiệp vụ để cả file không bị 500.
    private static IReadOnlyList<LanguageSpan> ParseTagged(string tagged)
    {
        try
        {
            return CodeSwitchText.Parse(tagged);
        }
        catch (FormatException ex)
        {
            throw new UnprocessableException("invalid_text_format", $"Câu trộn không gán nhãn được: {ex.Message}");
        }
    }

    private static string Tag(string raw)
    {
        if (raw.Contains(CodeSwitchText.ViTag) || raw.Contains(CodeSwitchText.EnTag))
        {
            throw new UnprocessableException(
                "invalid_text_format",
                "File .txt là câu thô, không kèm nhãn [vi]/[en]. Bỏ nhãn rồi nhập lại.");
        }

        var builder = new StringBuilder(raw.Length + 16);
        var last = 0;
        bool? currentIsEnglish = null;

        foreach (Match match in TokenPattern.Matches(raw))
        {
            builder.Append(raw, last, match.Index - last);

            var isEnglish = IsEnglishToken(match.Value) ?? currentIsEnglish ?? false;

            if (currentIsEnglish != isEnglish)
            {
                builder.Append(isEnglish ? CodeSwitchText.EnTag : CodeSwitchText.ViTag);
                currentIsEnglish = isEnglish;
            }

            builder.Append(match.Value);
            last = match.Index + match.Length;
        }

        builder.Append(raw, last, raw.Length - last);

        return builder.ToString().Trim();
    }

    /// <summary>null = dấu câu/số, giữ nguyên ngôn ngữ của đoạn đang chạy.</summary>
    private static bool? IsEnglishToken(string token)
    {
        var letters = new StringBuilder(token.Length);
        var hasDigit = false;

        foreach (var ch in token)
        {
            if (char.IsLetter(ch)) letters.Append(ch);
            else if (char.IsDigit(ch)) hasDigit = true;
        }

        if (letters.Length == 0) return null;

        foreach (var ch in letters.ToString())
        {
            if (ch > 127) return false;
        }

        // Một chữ cái đơn lẻ là "a"/"I" hiếm hơn nhiều so với từ Việt không dấu; bỏ qua.
        if (letters.Length == 1 && !hasDigit) return false;

        return !VietnameseStopWords.Contains(letters.ToString());
    }
}
