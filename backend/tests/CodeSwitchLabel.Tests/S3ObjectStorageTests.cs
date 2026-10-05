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
[Trait("Category", "Unit")]
public class S3ObjectStorageTests
{
    private const string Internal = "http://minio:9000";
    private const string Public = "http://localhost:9000";

    private static S3ObjectStorage Create(string serviceUrl, string? publicUrl, string bucket = "recordings")
    {
        var options = new ObjectStorageOptions
        {
            ServiceUrl = serviceUrl,
            PublicUrl = publicUrl,
            AccessKey = "test",
            SecretKey = "test-secret",
            BucketName = bucket,
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
        // Arrange — bucket "recordings" + key bắt đầu bằng "recordings/" nên URL có hai đoạn
        // "recordings/recordings/..." là đúng, không phải lỗi nối chuỗi.
        using var storage = Create(Internal, Public);

        // Act
        var (url, _) = await storage.GetDownloadUrlAsync("recordings/2026/09/r_cs_211000001.wav", TestContext.Current.CancellationToken);

        // Assert — chỉ kiểm tiền tố: phần còn lại là chữ ký có thời hạn, mỗi lần mỗi khác.
        Assert.StartsWith("http://localhost:9000/recordings/recordings/2026/09/r_cs_211000001.wav", url);
    }

    [Fact]
    public void DiaChiLuuVaoDatabase_DungDiaChiCongKhai()
    {
        // Arrange
        using var storage = Create(Internal, Public);

        // Act
        var url = storage.GetObjectUrl("recordings/2026/09/r_vi_211000001.wav");

        // Assert
        Assert.Equal(
            "http://localhost:9000/recordings/recordings/2026/09/r_vi_211000001.wav",
            url);
    }

    [Fact]
    public void KhongDatDiaChiCongKhai_DungLuonDiaChiNoiBo()
    {
        // Arrange
        using var storage = Create(Public, null);

        // Act
        var url = storage.GetObjectUrl("a.wav");

        // Assert
        Assert.StartsWith("http://localhost:9000/", url);
    }

    /// <summary>Rủi ro R5: đổi domain lúc deploy không được làm hỏng link của bản ghi cũ.</summary>
    [Theory]
    [InlineData("http://localhost:9000/recordings/recordings/2026/09/r_cs_211000001.wav")]
    [InlineData("https://files.codeswitchlabel.vn/recordings/recordings/2026/09/r_cs_211000001.wav")]
    [InlineData("http://minio:9000/recordings/recordings/2026/09/r_cs_211000001.wav")]
    public void LayKhoaFile_KhongPhuThuocTenMayChu(string storedUrl)
    {
        // Arrange
        using var storage = Create(Internal, Public);

        // Act
        var key = storage.GetObjectKey(storedUrl);

        // Assert
        Assert.Equal("recordings/2026/09/r_cs_211000001.wav", key);
    }

    [Theory]
    [InlineData("http://localhost:9000/bucket-khac/a.wav")]
    [InlineData("http://localhost:9000/recordings/")]
    [InlineData("khong-phai-url")]
    public void DiaChiKhongThuocBucket_TraNull(string storedUrl)
    {
        // Arrange
        using var storage = Create(Internal, Public);

        // Act
        var key = storage.GetObjectKey(storedUrl);

        // Assert
        Assert.Null(key);
    }

    [Theory]
    [InlineData("/recordings/2026/09/r_cs_1.wav")] // key có dấu / ở đầu
    [InlineData("")] // key rỗng
    public void GetObjectUrl_VoiKhoaBien_DungDangPathStyle(string key)
    {
        // Arrange
        using var storage = Create(Internal, Public);

        // Act
        var url = storage.GetObjectUrl(key);

        // Assert — không ném, luôn ra URL dưới bucket đã cấu hình.
        Assert.StartsWith("http://localhost:9000/recordings/", url);
    }

    [Theory]
    [InlineData("r_cs_211000001.wav")]
    [InlineData("recordings/2026/09/r_cs_211000001.wav")]
    [InlineData("2026/09/r_cs với dấu cách.wav")]
    public void GetObjectKey_VoiKeyMaHoaUrl_TraDungKhoaGoc(string key)
    {
        // Arrange
        using var storage = Create(Internal, Public);
        var stored = storage.GetObjectUrl(key);

        // Act
        var roundTripped = storage.GetObjectKey(stored);

        // Assert
        Assert.Equal(key.TrimStart('/'), roundTripped);
    }
}
