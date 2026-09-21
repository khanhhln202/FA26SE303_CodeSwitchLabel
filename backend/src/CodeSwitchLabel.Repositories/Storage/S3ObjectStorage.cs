using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Repositories.Storage;

/// <summary>Cài đặt kho lưu file theo chuẩn S3 bằng AWS SDK.</summary>
public sealed class S3ObjectStorage(
    IAmazonS3 s3,
    IOptions<ObjectStorageOptions> options,
    TimeProvider clock,
    ILogger<S3ObjectStorage> logger) : IObjectStorage
{
    private readonly ObjectStorageOptions _options = options.Value;

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        if (await AmazonS3Util.DoesS3BucketExistV2Async(s3, _options.BucketName))
        {
            return;
        }

        await s3.PutBucketAsync(new PutBucketRequest { BucketName = _options.BucketName }, ct);
        logger.LogInformation("Đã tạo bucket {Bucket}", _options.BucketName);
    }

    public async Task UploadAsync(string key, string filePath, string contentType, CancellationToken ct = default)
    {
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            FilePath = filePath,
            ContentType = contentType
        }, ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await s3.DeleteObjectAsync(_options.BucketName, key, ct);
    }

    public string GetObjectUrl(string key) => $"{Prefix()}{key}";

    public string? GetObjectKey(string url)
    {
        var prefix = Prefix();

        return url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? url[prefix.Length..]
            : null;
    }

    /// <summary>
    /// Phần đầu cố định của mọi địa chỉ file. Kiểu đường dẫn thì bucket nằm sau tên miền,
    /// kiểu tên miền con thì bucket nằm trong tên miền.
    /// </summary>
    private string Prefix()
    {
        var service = _options.ServiceUrl.TrimEnd('/');

        if (_options.ForcePathStyle) return $"{service}/{_options.BucketName}/";

        var uri = new Uri(service);
        return $"{uri.Scheme}://{_options.BucketName}.{uri.Authority}/";
    }

    public async Task<(string Url, DateTimeOffset ExpiresAt)> GetDownloadUrlAsync(
        string key, CancellationToken ct = default)
    {
        var expiresAt = clock.GetUtcNow().AddMinutes(_options.PresignedUrlMinutes);

        var url = await s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,

            // SDK mặc định sinh link https. Server lúc phát triển chạy http, nên phải
            // bám theo địa chỉ trong cấu hình — nếu không, link trả về sẽ không mở được.
            Protocol = _options.ServiceUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase)
                ? Protocol.HTTPS
                : Protocol.HTTP
        });

        return (url, expiresAt);
    }
}
