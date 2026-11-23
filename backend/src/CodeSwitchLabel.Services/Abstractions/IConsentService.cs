using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface IConsentService
{
    /// <summary>Ghi nhận (hoặc cập nhật) đồng ý cam kết của người dùng.</summary>
    Task<ConsentDto> RecordConsentAsync(long userId, RecordConsentRequest request, CancellationToken ct = default);

    /// <summary>Lấy thông tin đồng ý cam kết hiện tại của người dùng, null nếu chưa có.</summary>
    Task<ConsentDto?> GetConsentAsync(long userId, CancellationToken ct = default);
}
