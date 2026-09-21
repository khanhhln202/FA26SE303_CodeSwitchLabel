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
        new(s.ScriptId, s.CsContent, s.VeContent, s.Status, s.Domain, s.WordCount, s.EnWordCount, s.CreatedAt);

    public static ScriptDetailDto ToDetail(this Script s) =>
        new(s.ScriptId,
            s.CsContent,
            SafeStrip(s.CsContent),
            s.VeContent,
            SafeStrip(s.VeContent),
            ScriptService.ReadAlignment(s.Alignment),
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
                r.EditedCsContent, r.EditedVeContent, r.Comment, r.ReviewedAt))]);

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
    ISystemConfigService config,
    ITaskProgressTracker taskTracker,
    TimeProvider clock) : IScriptService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

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

    public Task<ScriptDetailDto> CreateAsync(
        CreateScriptRequest request, long createdById, CancellationToken ct = default) =>
        CreateInternalAsync(request, createdById, ScriptStatus.Validated, null, ct);

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
                    Relation = RelationOf(item.Alignment),
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
                script.Status = ScriptStatus.Validated;
                break;

            case ScriptReviewAction.Edited:
                var (cs, ve) = RequireEditedPair(request);
                review.EditedCsContent = cs;
                review.EditedVeContent = ve;
                await ApplyEditAsync(script, cs, ve, ct);
                break;

            case ScriptReviewAction.Rejected:
                review.ErrorReasonId = await ResolveErrorReasonIdAsync(request.ErrorReasonCode, ct);
                script.Status = ScriptStatus.Rejected;
                break;
        }

        repository.AddReview(review);

        // updated_at do trigger trg_script_touch của database tự đặt.
        await repository.SaveChangesAsync(ct);

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
        var relation = request.Relation ?? ScriptRelation.DirectTranslation;

        // Mã do database sinh: số thứ tự nằm trong sequence, và ba chữ số đầu phải khớp
        // với en_word_count cùng chủ đề, nếu không ràng buộc CHECK sẽ từ chối.
        var scriptId = await repository.GenerateIdAsync(enWordCount, domain, relation, ct);
        var now = clock.GetUtcNow();

        var script = new Script
        {
            ScriptId = scriptId,
            CsContent = cs,
            VeContent = ve,
            Alignment = request.Alignment.Count == 0
                ? null
                : JsonSerializer.Serialize(request.Alignment, JsonOptions),
            Status = status,
            WordCount = wordCount,
            EnWordCount = enWordCount,
            Domain = domain,
            CreatedBy = userId,
            ImportBatchId = batchId,
            CreatedAt = now,
            UpdatedAt = now
        };

        repository.Add(script);
        await repository.SaveChangesAsync(ct);

        return script.ToDetail();
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
    private async Task ApplyEditAsync(Script script, string cs, string ve, CancellationToken ct)
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

        script.CsContent = cs;
        script.VeContent = ve;
        script.WordCount = wordCount;
        script.Status = ScriptStatus.Validated;
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

    /// <summary>Có một cặp từ là danh từ riêng thì cả câu tính mã quan hệ 2.</summary>
    private static ScriptRelation RelationOf(List<AlignmentItem> alignment) =>
        alignment.Any(a => a.Relation.Contains("proper", StringComparison.OrdinalIgnoreCase))
            ? ScriptRelation.ProperNoun
            : ScriptRelation.DirectTranslation;

    internal static IReadOnlyList<AlignmentItem> ReadAlignment(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try { return JsonSerializer.Deserialize<List<AlignmentItem>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
