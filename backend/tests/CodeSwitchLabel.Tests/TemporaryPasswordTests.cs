using CodeSwitchLabel.Services.Common;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Mật khẩu tạm được Admin đọc hoặc chép cho người dùng, nên phải đủ mạnh mà vẫn không đọc nhầm được.
/// </summary>
public class TemporaryPasswordTests
{
    [Fact]
    public void DoDaiCoDinh_VaDuDieuKienMatKhauToiThieu()
    {
        var password = TemporaryPassword.Generate();

        Assert.Equal(TemporaryPassword.Length, password.Length);
        Assert.True(password.Length >= 8);
    }

    /// <summary>0/O và 1/l/I đọc qua điện thoại hay chép tay rất dễ nhầm.</summary>
    [Fact]
    public void KhongCoKyTuDeDocNham()
    {
        var sample = string.Concat(Enumerable.Range(0, 200).Select(_ => TemporaryPassword.Generate()));

        Assert.DoesNotContain(sample, c => "0O1lI".Contains(c));
        Assert.All(sample, c => Assert.True(char.IsAsciiLetterOrDigit(c)));
    }

    [Fact]
    public void MoiLanSinhMotMatKhauKhac()
    {
        var passwords = Enumerable.Range(0, 1000).Select(_ => TemporaryPassword.Generate()).ToHashSet();

        Assert.Equal(1000, passwords.Count);
    }
}
