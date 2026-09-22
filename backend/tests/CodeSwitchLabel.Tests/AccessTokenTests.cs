using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Options;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Token mang dấu của mật khẩu lúc cấp. Đổi hay cấp lại mật khẩu là dấu đổi, nên token cũ bị từ chối —
/// phần so sánh nằm ở AccountStateValidator, ở đây kiểm dấu và token có đúng như validator mong đợi không.
/// </summary>
public class AccessTokenTests
{
    private static readonly BcryptPasswordHasher Hasher = new();

    [Fact]
    public void CungMotBanBam_LuonRaCungMotDau()
    {
        var hash = Hasher.Hash("MatKhau2026");

        Assert.Equal(PasswordStamp.From(hash), PasswordStamp.From(hash));
    }

    /// <summary>
    /// bcrypt gắn muối ngẫu nhiên vào mỗi lần băm: đặt lại đúng mật khẩu cũ vẫn ra bản băm mới,
    /// nên token cấp trước đó vẫn bị thu hồi.
    /// </summary>
    [Fact]
    public void BamLaiCungMotMatKhau_VanRaDauKhac()
    {
        Assert.NotEqual(PasswordStamp.From(Hasher.Hash("MatKhau2026")), PasswordStamp.From(Hasher.Hash("MatKhau2026")));
    }

    /// <summary>Dấu nằm trong token mà ai cầm token cũng đọc được, nên chỉ là 16 ký tự hex — không để lộ bản băm.</summary>
    [Fact]
    public void DauLa16KyTuHex()
    {
        Assert.Matches("^[0-9a-f]{16}$", PasswordStamp.From(Hasher.Hash("MatKhau2026")));
    }

    [Fact]
    public void TokenMangDauCuaMatKhauHienTai_CungMaNguoiDungVaVai()
    {
        var user = NewUser(Hasher.Hash("MatKhau2026"));

        var jwt = Read(NewIssuer().Issue(user).AccessToken);

        Assert.Equal("8", jwt.Subject);
        Assert.Equal("Speaker", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(PasswordStamp.From(user.PasswordHash), jwt.Claims.Single(c => c.Type == PasswordStamp.ClaimType).Value);
    }

    /// <summary>Đúng thứ validator làm ở mỗi request: tính dấu từ mật khẩu hiện tại rồi so với dấu trong token.</summary>
    [Fact]
    public void DoiMatKhau_DauTrongTokenCuKhongConKhop()
    {
        var user = NewUser(Hasher.Hash("MatKhau2026"));
        var oldToken = Read(NewIssuer().Issue(user).AccessToken);

        user.PasswordHash = Hasher.Hash("MatKhauMoi2027");

        Assert.NotEqual(PasswordStamp.From(user.PasswordHash), oldToken.Claims.Single(c => c.Type == PasswordStamp.ClaimType).Value);
    }

    [Fact]
    public void HanTokenTinhTheoCauHinh()
    {
        var now = new DateTimeOffset(2026, 9, 22, 8, 0, 0, TimeSpan.Zero);

        var issued = NewIssuer(new FixedClock(now)).Issue(NewUser(Hasher.Hash("MatKhau2026")));

        Assert.Equal(now.AddMinutes(120), issued.ExpiresAt);
        Assert.Equal(issued.ExpiresAt.UtcDateTime, Read(issued.AccessToken).ValidTo);
    }

    // ----------------------------------------------------------------- dựng dữ liệu

    private static AppUser NewUser(string passwordHash) => new()
    {
        UserId = 8,
        Email = "speaker3@codeswitchlabel.local",
        FullName = "Người đọc số 3",
        PasswordHash = passwordHash,
        Role = new Role { RoleName = RoleName.Speaker }
    };

    private static JwtAccessTokenIssuer NewIssuer(TimeProvider? clock = null) => new(
        Options.Create(new JwtOptions
        {
            Issuer = "codeswitchlabel-test",
            Audience = "codeswitchlabel-test",
            Key = "khoa-ky-chi-dung-trong-unit-test-du-32-ky-tu",
            ExpiryMinutes = 120
        }),
        clock ?? TimeProvider.System);

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
