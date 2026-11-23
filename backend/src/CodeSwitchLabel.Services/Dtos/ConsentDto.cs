using System.ComponentModel.DataAnnotations;

namespace CodeSwitchLabel.Services.Dtos;

/// <summary>Thông tin đồng ý cam kết của một người dùng (lưu trong bảng user_consent).</summary>
public record ConsentDto(
    long UserId,
    string Version,
    DateTimeOffset AcceptedAt,
    string? DocumentKey,
    string? DocumentName);

/// <summary>Yêu cầu ghi nhận đồng ý cam kết sử dụng dữ liệu sinh trắc âm thanh.</summary>
public record RecordConsentRequest
{
    /// <summary>Phải bằng true — người dùng chủ động tích vào checkbox xác nhận tự nguyện.</summary>
    [Required(ErrorMessage = "Phải xác nhận đồng ý cam kết trước khi tiếp tục.")]
    public bool ConsentAccepted { get; init; }

    /// <summary>Phiên bản của bản cam kết (ví dụ "v1").</summary>
    [Required(ErrorMessage = "Phải chỉ định phiên bản cam kết.")]
    [StringLength(32)]
    public string ConsentVersion { get; init; } = string.Empty;
}
