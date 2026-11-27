using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface IConsentService
{
    /// <summary>Ghi nhận (hoặc cập nhật) đồng ý cam kết của người dùng.</summary>
    Task<ConsentDto> RecordConsentAsync(long userId, RecordConsentRequest request, CancellationToken ct = default);

    /// <summary>Lấy thông tin đồng ý cam kết hiện tại của người dùng, null nếu chưa có.</summary>
    Task<ConsentDto?> GetConsentAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// Nội dung bản cam kết ĐANG HIỆU LỰC — thứ người dùng phải đọc trước khi bấm đồng ý.
    /// Ném 404 khi chưa có bản nào: không có chữ thì không được mời ai đồng ý.
    /// </summary>
    Task<ConsentDocumentDto> GetActiveDocumentAsync(CancellationToken ct = default);

    /// <summary>
    /// Nội dung một phiên bản cụ thể, kể cả bản đã cũ — để người đã ký đọc lại đúng chữ mình
    /// từng đồng ý. Null nếu không có phiên bản đó.
    /// </summary>
    Task<ConsentDocumentDto?> GetDocumentAsync(string version, CancellationToken ct = default);

    /// <summary>Mọi phiên bản, mới nhất trước.</summary>
    Task<IReadOnlyList<ConsentDocumentDto>> ListDocumentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Phát hành phiên bản mới; bản đang hiệu lực tự lui thành bản cũ.
    ///
    /// <para>
    /// KHÔNG sửa bản cũ: phiên bản đã có người đồng ý là bằng chứng, đổi chữ trong đó là làm
    /// sai lệch thứ người ta đã ký. Trùng mã phiên bản thì trả 409.
    /// </para>
    /// </summary>
    Task<ConsentDocumentDto> PublishDocumentAsync(
        PublishConsentDocumentRequest request, long createdBy, CancellationToken ct = default);
}
