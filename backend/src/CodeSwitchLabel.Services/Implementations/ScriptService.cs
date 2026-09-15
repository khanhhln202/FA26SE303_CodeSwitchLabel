using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Implementations;

internal static class ScriptMapper
{
    public static ScriptListItemDto ToListItem(this Script s) =>
        new(s.ScriptId, s.Content, s.Status, s.Domain, s.WordCount, s.EnWordCount, s.CreatedAt);

    public static ScriptDetailDto ToDetail(this Script s) =>
        new(s.ScriptId, s.Content, s.Status, s.Domain, s.WordCount, s.EnWordCount,
            s.CreatedById, s.ImportBatchId, s.CreatedAt, s.UpdatedAt,
            [.. s.Reviews.Select(r => new ScriptReviewDto(
                r.ScriptReviewId, r.UserId, r.Action, r.ErrorReason?.ReasonCode,
                r.EditedContent, r.Comment, r.ReviewedAt))]);
}

public class ScriptService(
    IScriptRepository repository,
    IReasonRepository reasonRepository,
    ISystemConfigService config,
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

    public async Task<ScriptDetailDto> GetAsync(long id, CancellationToken ct = default)
    {
        var script = await repository.GetWithReviewsAsync(id, ct)
                     ?? throw NotFoundException.Script(id);

        return script.ToDetail();
    }

    public Task<ScriptDetailDto> CreateAsync(
        CreateScriptRequest request, long createdById, CancellationToken ct = default) =>
        CreateInternalAsync(request, createdById, ScriptStatus.Validated, ct);

    public Task<ScriptDetailDto> ContributeAsync(
        CreateScriptRequest request, long contributorId, CancellationToken ct = default) =>
        CreateInternalAsync(request, contributorId, ScriptStatus.PendingValidation, ct);

    /// <summary>
    /// Admin tạo thì vào thẳng lưu thông; Speaker đóng góp thì nằm chờ duyệt.
    /// Khác nhau đúng một tham số nên dùng chung thân hàm.
    /// </summary>
    private async Task<ScriptDetailDto> CreateInternalAsync(
        CreateScriptRequest request, long userId, ScriptStatus status, CancellationToken ct)
    {
        var content = request.Content.Trim();

        var wordCount = ScriptTextNormalizer.CountWords(content);

        // Người nhập biết rõ hơn máy đoán, nên cho phép ghi đè con số ước lượng.
        var enWordCount = request.EnWordCount ?? ScriptTextNormalizer.CountEnglishWords(content);

        await ValidateAsync(content, wordCount, enWordCount, ct);

        if (await repository.ContentExistsAsync(content, ct))
        {
            throw ConflictException.DuplicateContent();
        }

        var now = clock.GetUtcNow();

        var script = new Script
        {
            Content = content,
            Status = status,
            WordCount = wordCount,
            EnWordCount = enWordCount,
            Domain = request.Domain!.Value,
            CreatedById = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        repository.Add(script);
        await repository.SaveChangesAsync(ct);

        return script.ToDetail();
    }

    public async Task<ScriptDetailDto> ReviewAsync(
        long scriptId, long userId, ReviewScriptRequest request, CancellationToken ct = default)
    {
        var script = await repository.GetForUpdateAsync(scriptId, ct)
                     ?? throw NotFoundException.Script(scriptId);

        if (script.Status is not (ScriptStatus.PendingValidation or ScriptStatus.Validated))
        {
            throw new ConflictException(
                "script_not_reviewable",
                $"Script #{scriptId} đang là {script.Status} nên không duyệt nội dung được nữa.");
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
                review.EditedContent = RequireEditedContent(request);
                await ApplyEditAsync(script, review.EditedContent, ct);
                break;

            case ScriptReviewAction.Rejected:
                review.ErrorReasonId = await ResolveErrorReasonIdAsync(request.ErrorReasonCode, ct);
                script.Status = ScriptStatus.Rejected;
                break;
        }

        script.UpdatedAt = now;

        repository.AddReview(review);
        await repository.SaveChangesAsync(ct);

        return (await repository.GetWithReviewsAsync(scriptId, ct))!.ToDetail();
    }

    private static string RequireEditedContent(ReviewScriptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EditedContent))
        {
            throw new UnprocessableException(
                "edited_content_required",
                "Chọn kết quả 'Edited' thì phải gửi kèm nội dung đã sửa.");
        }

        return request.EditedContent.Trim();
    }

    /// <summary>
    /// Ghi nội dung mới đè lên script, và tính lại hai cột đếm.
    ///
    /// Lưu ý về giới hạn của thiết kế hiện tại: lược đồ không có bảng lưu phiên bản,
    /// nên nội dung cũ không được giữ lại. Đường bảo vệ duy nhất là chỉ cho sửa
    /// khi script chưa có bản ghi âm nào — kiểm ngay dưới đây.
    /// </summary>
    private async Task ApplyEditAsync(Script script, string editedContent, CancellationToken ct)
    {
        if (await repository.HasRecordingsAsync(script.ScriptId, ct))
        {
            throw new ConflictException(
                "script_already_recorded",
                $"Script #{script.ScriptId} đã có bản ghi âm nên không sửa nội dung được. " +
                "Sửa sẽ làm transcript của các bản ghi cũ không còn khớp với âm thanh.");
        }

        script.Content = editedContent;
        script.WordCount = ScriptTextNormalizer.CountWords(editedContent);
        script.EnWordCount = ScriptTextNormalizer.CountEnglishWords(editedContent);
        script.Status = ScriptStatus.Validated;
    }

    private async Task<short> ResolveErrorReasonIdAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new UnprocessableException(
                "error_reason_required",
                "Từ chối script thì bắt buộc nêu lý do.");
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

    private async Task ValidateAsync(string content, int wordCount, int enWordCount, CancellationToken ct)
    {
        var minWords = await config.GetIntAsync(ConfigKeys.ScriptMinWordCount, 4, ct);
        var maxWords = await config.GetIntAsync(ConfigKeys.ScriptMaxWordCount, 40, ct);
        var minEnWords = await config.GetIntAsync(ConfigKeys.ScriptMinEnWordCount, 1, ct);

        if (wordCount < minWords)
        {
            throw new UnprocessableException("script_too_short",
                $"Script có {wordCount} từ, ngắn hơn mức tối thiểu {minWords} từ.");
        }

        if (wordCount > maxWords)
        {
            throw new UnprocessableException("script_too_long",
                $"Script có {wordCount} từ, dài hơn mức tối đa {maxWords} từ.");
        }

        if (enWordCount > wordCount)
        {
            throw new UnprocessableException("en_word_count_invalid",
                $"Số từ tiếng Anh ({enWordCount}) không thể lớn hơn tổng số từ ({wordCount}).");
        }

        // Câu không có từ tiếng Anh nào thì không phục vụ mục tiêu thu dữ liệu
        // trộn ngôn ngữ của đề tài. Ngưỡng đặt trong cấu hình để Admin đổi được.
        if (enWordCount < minEnWords)
        {
            throw new UnprocessableException("not_code_switched",
                $"Script phải có ít nhất {minEnWords} từ tiếng Anh, hiện chỉ có {enWordCount}.");
        }
    }
}
