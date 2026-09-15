using System.ComponentModel.DataAnnotations;

namespace CodeSwitchLabel.Repositories.Storage;

/// <summary>
/// Cấu hình kết nối kho lưu file. Dùng chuẩn S3 nên cùng một bộ tham số chạy được
/// với MinIO, Garage, RustFS hay AWS S3 thật — đổi nhà cung cấp chỉ đổi giá trị, không đổi code.
/// </summary>
public class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>Địa chỉ server S3, ví dụ http://localhost:9000.</summary>
    [Required, Url]
    public string ServiceUrl { get; set; } = string.Empty;

    [Required]
    public string AccessKey { get; set; } = string.Empty;

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    public string BucketName { get; set; } = "recordings";

    /// <summary>Server tự host không có vùng thật, nhưng chữ ký của chuẩn S3 vẫn bắt buộc có tên vùng.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// true: địa chỉ dạng http://server/bucket/key. false: http://bucket.server/key.
    /// Server tự host gần như luôn cần true, vì không có tên miền con riêng cho từng bucket.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    [Range(1, 1440)]
    public int PresignedUrlMinutes { get; set; } = 15;
}

/// <summary>
/// Kho lưu file âm thanh. Tầng Service chỉ biết interface này, không biết phía sau
/// là server nào — nên đổi nhà cung cấp không đụng tới một dòng nghiệp vụ nào.
/// </summary>
public interface IObjectStorage
{
    /// <summary>Tạo bucket nếu chưa có. Gọi một lần lúc khởi động.</summary>
    Task EnsureBucketAsync(CancellationToken ct = default);

    Task UploadAsync(string key, string filePath, string contentType, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Link tải tạm thời, tự hết hạn. Trình duyệt tải thẳng từ kho lưu trữ —
    /// API không làm trung gian truyền file nên không phải gánh băng thông audio.
    /// </summary>
    Task<(string Url, DateTimeOffset ExpiresAt)> GetDownloadUrlAsync(string key, CancellationToken ct = default);
}
