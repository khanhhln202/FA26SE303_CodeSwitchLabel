using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Repositories.Storage;

/// <summary>Cài đặt kho lưu file theo chuẩn S3 bằng AWS SDK.</summary>
public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly IAmazonS3 _s3;
    private readonly IAmazonS3 _presigner;
    private readonly bool _ownsPresigner;
    private readonly ObjectStorageOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<S3ObjectStorage> _logger;

    public S3ObjectStorage(
        IAmazonS3 s3,
        IOptions<ObjectStorageOptions> options,
        TimeProvider clock,
        ILogger<S3ObjectStorage> logger)
    {
        _s3 = s3;
        _options = options.Value;
        _clock = clock;
        _logger = logger;

        // Hai địa chỉ giống nhau thì dùng chung một client. Khác nhau thì thêm một client CHỈ để ký link:
        // ký link không gọi mạng, nên client này không cần kết nối được tới kho.
        if (string.Equals(PublicBaseUrl(), _options.ServiceUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            _presigner = s3;
        }
        else
        {
            _presigner = new AmazonS3Client(
                new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = PublicBaseUrl(),
                    ForcePathStyle = _options.ForcePathStyle,
                    AuthenticationRegion = _options.Region
                });
            _ownsPresigner = true;
        }
    }

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        if (await AmazonS3Util.DoesS3BucketExistV2Async(_s3, _options.BucketName))
        {
            return;
        }

        await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _options.BucketName }, ct);
        _logger.LogInformation("Đã tạo bucket {Bucket}", _options.BucketName);
    }

    public async Task UploadAsync(string key, string filePath, string contentType, CancellationToken ct = default)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            FilePath = filePath,
            ContentType = contentType
        }, ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _s3.DeleteObjectAsync(_options.BucketName, key, ct);
    }

    public string GetObjectUrl(string key)
    {
        var baseUrl = PublicBaseUrl();

        if (_options.ForcePathStyle) return $"{baseUrl}/{_options.BucketName}/{key}";

        var uri = new Uri(baseUrl);
        return $"{uri.Scheme}://{_options.BucketName}.{uri.Authority}/{key}";
    }

    public string? GetObjectKey(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');

        // Chỉ nhìn phần đường dẫn, bỏ qua tên máy chủ: địa chỉ lưu từ lúc chạy localhost
        // vẫn đọc được sau khi deploy lên domain thật.
        if (_options.ForcePathStyle)
        {
            var bucketPrefix = _options.BucketName + "/";

            return path.StartsWith(bucketPrefix, StringComparison.Ordinal) && path.Length > bucketPrefix.Length
                ? path[bucketPrefix.Length..]
                : null;
        }

        // Kiểu tên miền con: bucket nằm trong tên miền, đường dẫn chính là khoá.
        return uri.Host.StartsWith(_options.BucketName + ".", StringComparison.OrdinalIgnoreCase) && path.Length > 0
            ? path
            : null;
    }

    public async Task<(string Url, DateTimeOffset ExpiresAt)> GetDownloadUrlAsync(
        string key, CancellationToken ct = default)
    {
        var expiresAt = _clock.GetUtcNow().AddMinutes(_options.PresignedUrlMinutes);

        var url = await _presigner.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,

            // SDK mặc định sinh link https. Máy dev chạy http, nên bám theo địa chỉ công khai trong cấu hình.
            Protocol = PublicBaseUrl().StartsWith("https", StringComparison.OrdinalIgnoreCase)
                ? Protocol.HTTPS
                : Protocol.HTTP
        });

        return (url, expiresAt);
    }

    public void Dispose()
    {
        if (_ownsPresigner) _presigner.Dispose();
    }

    private string PublicBaseUrl() =>
        (string.IsNullOrWhiteSpace(_options.PublicUrl) ? _options.ServiceUrl : _options.PublicUrl).TrimEnd('/');
}
