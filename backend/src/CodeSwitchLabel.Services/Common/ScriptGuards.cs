using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;

namespace CodeSwitchLabel.Services.Common;

/// <summary>
/// hai chốt nghiệp vụ mà cả luồng duyệt nội dung lẫn luồng xử lý đề xuất
/// cùng đi qua, tách ra một chỗ để hai đường ghi không lệch nhau về sau.
///
/// Đây là code chuyển nguyên vẹn từ ScriptService, không đổi hành vi.
/// </summary>
internal static class ScriptGuards
{
    /// <summary>
    /// Người chốt nội dung câu phải có đúng chủ đề của câu đó. Kiểm ở đây để trả thông báo
    /// rõ ràng, thay vì để trigger trg_script_validated_domain ném lỗi thô từ database.
    /// </summary>
    public static async Task EnsureDomainQualifiedAsync(
        this IUserDomainRepository domainRepository, long userId, ScriptDomain domain, CancellationToken ct)
    {
        if (await domainRepository.IsQualifiedAsync(userId, domain, ct)) return;

        throw new UnprocessableException(
            "reviewer_domain_required",
            $"Chỉ người được phân đúng chủ đề {domain} mới duyệt được cặp câu này sang trạng thái đã duyệt. " +
            "Nhờ Admin phân chủ đề cho Reviewer trước (PUT /api/users/{id}/domains).");
    }

    /// <summary>
    /// Lý do lỗi phải có thật và còn dùng được. <paramref name="missingMessage"/> để mỗi luồng
    /// nói đúng việc của mình khi thiếu mã — câu báo lỗi là thứ người dùng đọc.
    /// </summary>
    public static async Task<short> ResolveActiveReasonIdAsync(
        this IReasonRepository reasonRepository, string? code, string missingMessage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new UnprocessableException("error_reason_required", missingMessage);
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
}
