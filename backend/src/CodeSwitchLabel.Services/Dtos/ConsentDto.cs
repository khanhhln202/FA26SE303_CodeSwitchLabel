using System.ComponentModel.DataAnnotations;

namespace CodeSwitchLabel.Services.Dtos;

/// <summary>Thông tin đồng ý cam kết của một người dùng (lưu trong bảng user_consent).</summary>
public record ConsentDto(
    long UserId,
    string Version,
    DateTimeOffset AcceptedAt,
    string? DocumentKey,
    string? DocumentName);

/// <summary>Nội dung một phiên bản bản cam kết (bảng consent_document).</summary>
/// <param name="Body">
/// Nguyên văn, GIỮ XUỐNG DÒNG. Giao diện phải hiện bằng khối giữ khoảng trắng
/// (<c>white-space: pre-wrap</c>), bóp về một dòng là mất bố cục điều khoản.
/// </param>
/// <param name="IsActive">
/// Bản đang hiệu lực. Bản cũ vẫn đọc được — đó là mục đích của bảng này: tra lại người
/// đã ký hồi đó đồng ý chữ gì.
/// </param>
public record ConsentDocumentDto(
    string Version,
    string Title,
    string Body,
    DateOnly EffectiveFrom,
    bool IsActive);

/// <summary>Phát hành một phiên bản cam kết mới; bản đang hiệu lực tự lui thành bản cũ.</summary>
public record PublishConsentDocumentRequest
{
    /// <summary>Mã phiên bản, ví dụ <c>v2</c>. Trùng mã đã có thì trả 409 — bản cũ không được ghi đè.</summary>
    [Required(ErrorMessage = "Phải đặt mã phiên bản.")]
    [StringLength(32, MinimumLength = 1)]
    public string Version { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phải có tiêu đề.")]
    [StringLength(255, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;

    /// <summary>Nguyên văn bản cam kết; xuống dòng giữ nguyên.</summary>
    [Required(ErrorMessage = "Phải có nội dung cam kết.")]
    [StringLength(100_000, MinimumLength = 1)]
    public string Body { get; init; } = string.Empty;

    /// <summary>Bỏ trống thì lấy ngày hôm nay.</summary>
    public DateOnly? EffectiveFrom { get; init; }
}

/// <summary>Yêu cầu ghi nhận đồng ý cam kết sử dụng dữ liệu sinh trắc âm thanh.</summary>
public record RecordConsentRequest
{
    /// <summary>Phải bằng true — người dùng chủ động tích vào checkbox xác nhận tự nguyện.</summary>
    [Required(ErrorMessage = "Phải xác nhận đồng ý cam kết trước khi tiếp tục.")]
    public bool ConsentAccepted { get; init; }

    /// <summary>
    /// Phiên bản bản cam kết, lấy từ <c>GET /api/consent</c>.
    ///
    /// <para>
    /// KHÔNG bắt buộc: bỏ trống thì server ghi theo bản ĐANG HIỆU LỰC. Client không cần biết
    /// mã phiên bản, và đó là chủ ý — trước đây trường này bắt buộc mà lại nhận chuỗi tự do,
    /// nên dòng cam kết duy nhất trên máy chủ thật mang phiên bản "string", tức giá trị mặc
    /// định của ô nhập trong Swagger.
    /// </para>
    ///
    /// <para>Có khai thì phải là phiên bản có nội dung thật, nếu không trả 422.</para>
    /// </summary>
    [StringLength(32)]
    public string? ConsentVersion { get; init; }
}
