using System.Text.Json;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.WorkTasks;

namespace CodeSwitchLabel.Services.Implementations;

internal static class ScriptMapper
{
    public static ScriptListItemDto ToListItem(this Script s) =>
        new(s.ScriptId, s.CsContent, s.ViContent, s.Status, s.Domain, s.WordCount, s.EnWordCount, s.CreatedAt);

    public static ScriptDetailDto ToDetail(this Script s) =>
        new(s.ScriptId,
            s.CsContent,
            SafeStrip(s.CsContent),
            s.ViContent,
            SafeStrip(s.ViContent),
            ScriptService.ToAlignment(s.Words),
            s.Status,
            s.Domain,
            s.WordCount,
            s.EnWordCount,
            s.CreatedBy,
            s.ImportBatchId,
            s.CreatedAt,
            s.UpdatedAt,
            [.. s.Reviews.Select(r => new ScriptReviewDto(
                r.ScriptReviewId, r.UserId, r.Action, r.ErrorReason?.ReasonCode,
                r.EditedCsContent, r.EditedViContent, r.Comment, r.ReviewedAt))]);

    /// <summary>Dữ liệu cũ hoặc dữ liệu ghi thẳng bằng SQL có thể thiếu nhãn — không vì thế mà vỡ API.</summary>
    private static string SafeStrip(string content)
    {
        try { return CodeSwitchText.Strip(content); }
        catch (FormatException) { return content; }
    }
}

public class ScriptService(
    IScriptRepository repository,
    IReasonRepository reasonRepository,
    IUserDomainRepository domainRepository,
    ISystemConfigService config,
    ITaskProgressTracker taskTracker,
    TimeProvider clock) : IScriptService
{
    public async Task<PagedResult<ScriptListItemDto>> SearchAsync(
        ScriptSearchRequest request, CancellationToken ct = default)
    {
        var (items, total) = await repository.SearchAsync(
            request.Status, request.Domain, request.Keyword, request.Page, request.PageSize, ct);

        return new PagedResult<ScriptListItemDto>(
            [.. items.Select(s => s.ToListItem())], request.Page, request.PageSize, total);
    }

    public async Task<ScriptDetailDto> GetAsync(string scriptId, CancellationToken ct = default)
    {
        var script = await repository.GetWithReviewsAsync(scriptId, ct)
                     ?? throw NotFoundException.Script(scriptId);

        return script.ToDetail();
    }

    /// <summary>
    /// Admin thêm tay một cặp câu. Lược đồ bắt buộc mọi cặp câu phải qua một lượt duyệt của
    /// người đủ trình độ đúng chủ đề mới chuyển sang đã duyệt, nên câu thêm tay cũng nằm CHỜ DUYỆT.
    /// </summary>
    public Task<ScriptDetailDto> CreateAsync(
        CreateScriptRequest request, long createdById, CancellationToken ct = default) =>
        CreateInternalAsync(request, createdById, ScriptStatus.PendingValidation, null, ct);

    public Task<ScriptDetailDto> ContributeAsync(
        CreateScriptRequest request, long contributorId, CancellationToken ct = default) =>
        CreateInternalAsync(request, contributorId, ScriptStatus.PendingValidation, null, ct);

    public async Task<ImportResultDto> ImportAsync(
        Stream jsonFile, string fileName, long importedById, CancellationToken ct = default)
    {
        var items = await ReadItemsAsync(jsonFile, ct);
        var cap = await config.GetIntAsync(ConfigKeys.ImportMaxScriptsPerBatch, 100_000, ct);

        if (items.Count > cap)
        {
            throw new UnprocessableException(
                "import_too_large", $"File có {items.Count} câu, vượt giới hạn {cap} câu mỗi lần nhập.");
        }

        var batch = new ImportBatch
        {
            ImportedBy = importedById,
            FileName = fileName,
            ScriptCount = 0,
            CreatedAt = clock.GetUtcNow()
        };

        repository.AddBatch(batch);
        await repository.SaveChangesAsync(ct);

        var skipped = new List<ImportSkippedDto>();
        var imported = new List<string>();

        foreach (var item in items)
        {
            try
            {
                var request = new CreateScriptRequest
                {
                    CsContent = item.CsTranscript,
                    VeContent = item.ViEquivalent,
                    Domain = ParseDomain(item.Domain),
                    Alignment = item.Alignment
                };

                // Câu nhập từ file vẫn phải qua bước duyệt nội dung, đúng mặc định của lược đồ.
                var created = await CreateInternalAsync(
                    request, importedById, ScriptStatus.PendingValidation, batch.BatchId, ct);

                imported.Add(created.ScriptId);
            }
            catch (AppException ex)
            {
                // Một câu hỏng không được làm hỏng cả file — ghi lại lý do rồi đi tiếp.
                skipped.Add(new ImportSkippedDto(item.Id, ex.Message));
            }
        }

        batch.ScriptCount = imported.Count;
        await repository.SaveChangesAsync(ct);

        return new ImportResultDto(batch.BatchId, fileName, imported.Count, imported, skipped);
    }

    public async Task<ScriptDetailDto> ReviewAsync(
        string scriptId, long userId, ReviewScriptRequest request, CancellationToken ct = default)
    {
        var script = await repository.GetForUpdateAsync(scriptId, ct)
                     ?? throw NotFoundException.Script(scriptId);

        if (script.Status is not (ScriptStatus.PendingValidation or ScriptStatus.Validated))
        {
            throw new ConflictException(
                "script_not_reviewable",
                $"Cặp câu {scriptId} đang là {script.Status} nên không duyệt nội dung được nữa.");
        }

        var action = request.Action!.Value;
        var now = clock.GetUtcNow();

        var review = new ScriptReview
        {
            ScriptId = scriptId,
            UserId = userId,
            Action = action,
            Comment = request.Comment,
            ReviewedAt = now
        };

        switch (action)
        {
            case ScriptReviewAction.Accepted:
                break;

            case ScriptReviewAction.Edited:
                var (cs, ve) = RequireEditedPair(request);
                review.EditedCsContent = cs;
                review.EditedViContent = ve;
                await ApplyEditAsync(script, request, cs, ve, ct);
                break;

            case ScriptReviewAction.Rejected:
                review.ErrorReasonId = await ResolveErrorReasonIdAsync(request.ErrorReasonCode, ct);
                script.Status = ScriptStatus.Rejected;
                break;
        }

        repository.AddReview(review);

        // Lưu lượt duyệt TRƯỚC khi chốt trạng thái validated: trigger trg_script_validated_domain
        // đòi hỏi đã có lượt duyệt của người đủ trình độ đúng chủ đề ngay lúc status đổi, mà trigger
        // không phải loại defer, nên lượt duyệt phải nằm sẵn trong database trước.
        await repository.SaveChangesAsync(ct);

        if (action is ScriptReviewAction.Accepted or ScriptReviewAction.Edited)
        {
            await EnsureDomainQualifiedAsync(userId, script.Domain, ct);

            script.Status = ScriptStatus.Validated;

            // updated_at do trigger trg_script_touch của database tự đặt.
            await repository.SaveChangesAsync(ct);
        }

        // Câu bị loại thì task nào đang chờ thu câu đó cũng mất mục ấy.
        if (action == ScriptReviewAction.Rejected)
        {
            await taskTracker.OnScriptRejectedAsync(scriptId, ct);
        }

        return (await repository.GetWithReviewsAsync(scriptId, ct))!.ToDetail();
    }

    // ----------------------------------------------------------------- nội bộ

    private async Task<ScriptDetailDto> CreateInternalAsync(
        CreateScriptRequest request, long userId, ScriptStatus status, long? batchId, CancellationToken ct)
    {
        var cs = request.CsContent.Trim();
        var ve = request.VeContent.Trim();

        var (wordCount, enWordCount) = ValidatePair(cs, ve);

        if (await repository.ContentExistsAsync(cs, ct))
        {
            throw ConflictException.DuplicateContent();
        }

        var domain = request.Domain!.Value;

        // Mỗi từ tiếng Anh phải có ĐÚNG một dòng script_word, nếu không trigger defer
        // trg_script_word_consistency sẽ chặn lúc COMMIT — nên alignment phải phủ đủ số từ.
        var words = BuildWords(request.Alignment, enWordCount);

        // Chữ số thứ ba của mã lấy từ chính các dòng script_word, không tin giá trị client gửi:
        // mã và bảng script_word phải luôn khớp nhau.
        var relation = RelationOf(words);

        // Mã do database sinh: số thứ tự nằm trong sequence, và ba chữ số đầu phải khớp
        // với en_word_count, chủ đề và quan hệ, nếu không ràng buộc sẽ từ chối.
        var scriptId = await repository.GenerateIdAsync(enWordCount, domain, relation, ct);
        var now = clock.GetUtcNow();

        var script = new Script
        {
            ScriptId = scriptId,
            CsContent = cs,
            ViContent = ve,
            Status = status,
            WordCount = wordCount,
            EnWordCount = enWordCount,
            Domain = domain,
            CreatedBy = userId,
            ImportBatchId = batchId,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var word in words) word.ScriptId = scriptId;

        repository.Add(script);
        repository.AddWords(words);
        await repository.SaveChangesAsync(ct);

        return (await repository.GetWithReviewsAsync(scriptId, ct))!.ToDetail();
    }

    /// <summary>
    /// Kiểm tra một cặp câu. Trả về tổng số từ của câu chen tiếng Anh và số từ tiếng Anh trong đó.
    /// </summary>
    private static (int WordCount, int EnWordCount) ValidatePair(string cs, string ve)
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

    private static (string Cs, string Ve) RequireEditedPair(ReviewScriptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EditedCsContent) ||
            string.IsNullOrWhiteSpace(request.EditedVeContent))
        {
            throw new UnprocessableException(
                "edited_pair_required",
                "Chọn Edited thì phải gửi cả câu chen tiếng Anh lẫn câu thuần Việt đã sửa — " +
                "lược đồ chỉ chấp nhận sửa theo cặp.");
        }

        return (request.EditedCsContent.Trim(), request.EditedVeContent.Trim());
    }

    /// <summary>
    /// Ghi nội dung mới đè lên cặp câu và tính lại hai cột đếm.
    /// Lược đồ không lưu phiên bản cũ, nên chỉ cho sửa khi chưa có bản ghi âm nào.
    /// Số từ tiếng Anh cũng không được đổi: nó nằm trong mã script, mà mã thì không đổi được.
    /// </summary>
    private async Task ApplyEditAsync(
        Script script, ReviewScriptRequest request, string cs, string ve, CancellationToken ct)
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
        if (request.EditedAlignment is { Count: > 0 } edited)
        {
            var words = BuildWords(edited, enWordCount);

            if (RelationOf(words) != RelationDigitOf(script.ScriptId))
            {
                throw new UnprocessableException(
                    "relation_locked",
                    $"Bản sửa đổi quan hệ Anh–Việt, nhưng quan hệ đã nằm trong mã {script.ScriptId} nên không đổi được.");
            }

            repository.RemoveWords(script.Words);
            foreach (var word in words) word.ScriptId = script.ScriptId;
            repository.AddWords(words);
        }

        script.CsContent = cs;
        script.ViContent = ve;
        script.WordCount = wordCount;
    }

    private async Task<short> ResolveErrorReasonIdAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new UnprocessableException("error_reason_required", "Từ chối cặp câu thì bắt buộc nêu lý do.");
        }

        var reason = await reasonRepository.GetScriptErrorReasonByCodeAsync(code, ct)
                     ?? throw new UnprocessableException(
                         "error_reason_unknown", $"Không có lý do nào mang mã \"{code}\".");

        if (!reason.IsActive)
        {
            throw new UnprocessableException(
                "error_reason_inactive", $"Lý do \"{code}\" đã bị ẩn, không dùng được nữa.");
        }

        return reason.ReasonId;
    }

    private static async Task<List<ImportScriptItem>> ReadItemsAsync(Stream file, CancellationToken ct)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(file, cancellationToken: ct);

            // Nhận cả file một câu lẫn file nhiều câu, vì mẫu của giảng viên là một phần tử rời.
            var json = document.RootElement.GetRawText();

            return document.RootElement.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<ImportScriptItem>>(json) ?? []
                : [JsonSerializer.Deserialize<ImportScriptItem>(json)!];
        }
        catch (JsonException ex)
        {
            throw new UnprocessableException("invalid_json", $"File không phải JSON hợp lệ: {ex.Message}");
        }
    }

    /// <summary>"IT/Technology", "daily life", "Daily_Life" đều hiểu được.</summary>
    private static ScriptDomain ParseDomain(string value)
    {
        var normalized = new string((value ?? string.Empty).Where(char.IsLetter).ToArray()).ToLowerInvariant();

        return normalized switch
        {
            "ittechnology" or "it" or "technology" => ScriptDomain.ItTechnology,
            "education" => ScriptDomain.Education,
            "dailylife" => ScriptDomain.DailyLife,
            _ => throw new UnprocessableException(
                "unknown_domain",
                $"Chủ đề \"{value}\" không thuộc ba chủ đề của hệ thống: IT/Technology, Education, Daily Life.")
        };
    }

    /// <summary>Có một từ là danh từ riêng thì cả câu mang mã quan hệ 2 (ProperNoun).</summary>
    private static ScriptRelation RelationOf(List<ScriptWord> words) =>
        words.Any(w => w.Relation == ScriptWordRelation.ProperNoun)
            ? ScriptRelation.ProperNoun
            : ScriptRelation.DirectTranslation;

    /// <summary>
    /// Quan hệ ghi trong mã câu: chữ số thứ ba (vị trí 5) là 2 khi câu có danh từ riêng.
    /// </summary>
    private static ScriptRelation RelationDigitOf(string scriptId) =>
        scriptId.Length >= 5 && scriptId[4] == '2'
            ? ScriptRelation.ProperNoun
            : ScriptRelation.DirectTranslation;

    /// <summary>
    /// Đổi alignment thành các dòng script_word — đúng một dòng cho mỗi từ tiếng Anh.
    /// Thiếu hay thừa dòng đều bị từ chối vì trigger defer của database buộc số dòng khớp en_word_count.
    /// </summary>
    private static List<ScriptWord> BuildWords(List<AlignmentItem> alignment, int enWordCount)
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
            var en = item.Source.Trim();

            if (en.Length == 0)
            {
                throw new UnprocessableException(
                    "alignment_word_missing", $"Mục alignment thứ {i + 1} thiếu từ tiếng Anh.");
            }

            var isProper = item.Relation.Contains("proper", StringComparison.OrdinalIgnoreCase);

            // Danh từ riêng giữ nguyên, không dịch: lược đồ bắt buộc vi_word = en_word.
            var vi = isProper ? en : item.Target.Trim();

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
    internal static IReadOnlyList<AlignmentItem> ToAlignment(IEnumerable<ScriptWord> words) =>
        [.. words
            .OrderBy(w => w.WordPosition)
            .Select(w => new AlignmentItem
            {
                Source = w.EnWord,
                Target = w.ViWord,
                Relation = SnakeCaseNaming.ToSnakeCase(w.Relation.ToString())
            })];

    /// <summary>
    /// Cặp câu chỉ được chuyển sang đã duyệt khi người duyệt có đúng chủ đề. Kiểm ở đây để trả
    /// thông báo rõ ràng, thay vì để trigger trg_script_validated_domain ném lỗi thô từ database.
    /// </summary>
    private async Task EnsureDomainQualifiedAsync(long userId, ScriptDomain domain, CancellationToken ct)
    {
        if (await domainRepository.IsQualifiedAsync(userId, domain, ct)) return;

        throw new UnprocessableException(
            "reviewer_domain_required",
            $"Chỉ người được phân đúng chủ đề {domain} mới duyệt được cặp câu này sang trạng thái đã duyệt. " +
            "Nhờ Admin phân chủ đề cho Reviewer trước (PUT /api/users/{id}/domains).");
    }
}
