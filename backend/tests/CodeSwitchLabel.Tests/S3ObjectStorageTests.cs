using Amazon.Runtime;
using Amazon.S3;
using CodeSwitchLabel.Repositories.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Khi API chạy trong Docker, nó gọi kho file qua địa chỉ nội bộ còn trình duyệt dùng địa chỉ khác.
/// Sai một trong hai là không nộp được file hoặc không nghe được. Các test này không cần mạng:
/// ký link và tính địa chỉ đều làm được ngay trong bộ nhớ.
/// </summary>
public class S3ObjectStorageTests
{
    private const string Internal = "http://minio:9000";
    private const string Public = "http://localhost:9000";

    private static S3ObjectStorage Create(string serviceUrl, string? publicUrl)
    {
        var options = new ObjectStorageOptions
        {
            ServiceUrl = serviceUrl,
            PublicUrl = publicUrl,
            AccessKey = "test",
            SecretKey = "test-secret",
            BucketName = "recordings",
            ForcePathStyle = true
        };

        var s3 = new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config { ServiceURL = serviceUrl, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });

        return new S3ObjectStorage(s3, Options.Create(options), TimeProvider.System, NullLogger<S3ObjectStorage>.Instance);
    }

    [Fact]
    public async Task LinkNgheTam_KyBangDiaChiTrinhDuyetThayDuoc()
    {
        using var storage = Create(Internal, Public);

        var (url, _) = await storage.GetDownloadUrlAsync("recordings/2026/09/r_cs_211000001.wav");

        Assert.StartsWith("http://localhost:9000/recordings/recordings/2026/09/r_cs_211000001.wav", url);
    }

    [Fact]
    public void DiaChiLuuVaoDatabase_DungDiaChiCongKhai()
    {
        using var storage = Create(Internal, Public);

        Assert.Equal(
            "http://localhost:9000/recordings/recordings/2026/09/r_vi_211000001.wav",
            storage.GetObjectUrl("recordings/2026/09/r_vi_211000001.wav"));
    }

    [Fact]
    public void KhongDatDiaChiCongKhai_DungLuonDiaChiNoiBo()
    {
        using var storage = Create(Public, null);

        Assert.StartsWith("http://localhost:9000/", storage.GetObjectUrl("a.wav"));
    }

    /// <summary>Rủi ro R5: đổi domain lúc deploy không được làm hỏng link của bản ghi cũ.</summary>
    [Theory]
    [InlineData("http://localhost:9000/recordings/recordings/2026/09/r_cs_211000001.wav")]
    [InlineData("https://files.codeswitchlabel.vn/recordings/recordings/2026/09/r_cs_211000001.wav")]
    [InlineData("http://minio:9000/recordings/recordings/2026/09/r_cs_211000001.wav")]
    public void LayKhoaFile_KhongPhuThuocTenMayChu(string storedUrl)
    {
        using var storage = Create(Internal, Public);

        Assert.Equal("recordings/2026/09/r_cs_211000001.wav", storage.GetObjectKey(storedUrl));
    }

    [Theory]
    [InlineData("http://localhost:9000/bucket-khac/a.wav")]
    [InlineData("http://localhost:9000/recordings/")]
    [InlineData("khong-phai-url")]
    public void DiaChiKhongThuocBucket_TraNull(string storedUrl)
    {
        using var storage = Create(Internal, Public);

        Assert.Null(storage.GetObjectKey(storedUrl));
    }
}
