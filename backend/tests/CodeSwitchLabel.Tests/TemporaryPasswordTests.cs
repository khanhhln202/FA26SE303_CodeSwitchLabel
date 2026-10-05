using CodeSwitchLabel.Services.Common;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Mật khẩu tạm được Admin đọc hoặc chép cho người dùng, nên phải đủ mạnh mà vẫn không đọc nhầm được.
/// </summary>
[Trait("Category", "Unit")]
public class TemporaryPasswordTests
{
    [Fact]
    public void DoDaiCoDinh_Dung12KyTu()
    {
        // Arrange + Act
        var password = TemporaryPassword.Generate();

        // Assert
        Assert.Equal(12, password.Length);
        Assert.Equal(TemporaryPassword.Length, password.Length);
    }

    /// <summary>0/O và 1/l/I đọc qua điện thoại hay chép tay rất dễ nhầm.</summary>
    [Fact]
    public void KhongCoKyTuDeDocNham()
    {
        // Arrange — 200 mật khẩu là đủ để mọi ký tự trong bảng đều xuất hiện nhiều lần.
        // Act
        var sample = string.Concat(Enumerable.Range(0, 200).Select(_ => TemporaryPassword.Generate()));

        // Assert
        Assert.DoesNotContain(sample, c => "0O1lI".Contains(c));
        Assert.All(sample, c => Assert.True(char.IsAsciiLetterOrDigit(c)));
    }

    [Fact]
    public void MoiLanSinhMotMatKhauKhac()
    {
        // Arrange + Act — RandomNumberGenerator: trùng là va chạm 70-bit, thực tế không xảy ra.
        var passwords = Enumerable.Range(0, 1000).Select(_ => TemporaryPassword.Generate()).ToHashSet();

        // Assert
        Assert.Equal(1000, passwords.Count);
    }

    [Fact]
    public void BangChuCai_ChiGom56KyTuAnToan()
    {
        // Arrange — lấy mẫu lớn để phủ hết bảng chữ cái rồi kiểm tập ký tự xuất hiện.
        // Act
        var sample = string.Concat(Enumerable.Range(0, 500).Select(_ => TemporaryPassword.Generate()));
        var distinct = sample.Distinct().OrderBy(c => c).ToArray();

        // Assert
        Assert.DoesNotContain(distinct, c => "0O1lI".Contains(c));
        Assert.All(distinct, c => Assert.True(char.IsAsciiLetterOrDigit(c)));
        Assert.True(distinct.Length > 40); // bảng 56 ký tự phải xuất hiện phần lớn sau 6000 lượt rút
    }

    [Fact]
    public void SinhSongSong_KhongTrungNhau()
    {
        // Arrange + Act — RandomNumberGenerator.GetString an toàn luồng, gọi song song vẫn khác nhau.
        var passwords = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 128, _ => passwords.Add(TemporaryPassword.Generate()));

        // Assert
        Assert.Equal(128, passwords.Distinct().Count());
    }
}
