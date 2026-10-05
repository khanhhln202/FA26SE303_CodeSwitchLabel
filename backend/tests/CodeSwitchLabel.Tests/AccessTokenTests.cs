using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Token mang dấu của mật khẩu lúc cấp. Đổi hay cấp lại mật khẩu là dấu đổi, nên token cũ bị từ chối —
/// phần so sánh nằm ở AccountStateValidator, ở đây kiểm dấu và token có đúng như validator mong đợi không.
/// </summary>
[Trait("Category", "Unit")]
public class AccessTokenTests
{
    private static readonly BcryptPasswordHasher Hasher = new();

    // Băm một lần cho cả class: bcrypt tốn ~2^11 vòng, băm trong từng test làm suite chậm đi nhiều.
    private static readonly string HashA = Hasher.Hash("MatKhau2026");
    private static readonly string HashB = Hasher.Hash("MatKhau2026");

    private const string TestKey = "khoa-ky-chi-dung-trong-unit-test-du-32-ky-tu";

    [Fact]
    public void CungMotBanBam_LuonRaCungMotDau()
    {
        // Arrange
        var hash = HashA;

        // Act
        var first = PasswordStamp.From(hash);
        var second = PasswordStamp.From(hash);

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    /// bcrypt gắn muối ngẫu nhiên vào mỗi lần băm: đặt lại đúng mật khẩu cũ vẫn ra bản băm mới,
    /// nên token cấp trước đó vẫn bị thu hồi.
    /// </summary>
    [Fact]
    public void BamLaiCungMotMatKhau_VanRaDauKhac()
    {
        // Arrange + Act
        var first = PasswordStamp.From(HashA);
        var second = PasswordStamp.From(HashB);

        // Assert
        Assert.NotEqual(first, second);
    }

    /// <summary>Dấu nằm trong token mà ai cầm token cũng đọc được, nên chỉ là 16 ký tự hex — không để lộ bản băm.</summary>
    [Fact]
    public void DauLa16KyTuHex()
    {
        // Arrange + Act
        var stamp = PasswordStamp.From(HashA);

        // Assert
        Assert.Matches("^[0-9a-f]{16}$", stamp);
    }

    [Fact]
    public void TokenMangDauCuaMatKhauHienTai_CungMaNguoiDungVaVai()
    {
        // Arrange
        var user = NewUser(HashA);

        // Act
        var jwt = Read(NewIssuer().Issue(user).AccessToken);

        // Assert
        Assert.Equal("8", jwt.Subject);
        Assert.Equal("Speaker", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(PasswordStamp.From(user.PasswordHash), jwt.Claims.Single(c => c.Type == PasswordStamp.ClaimType).Value);
    }

    /// <summary>Đúng thứ validator làm ở mỗi request: tính dấu từ mật khẩu hiện tại rồi so với dấu trong token.</summary>
    [Fact]
    public void DoiMatKhau_DauTrongTokenCuKhongConKhop()
    {
        // Arrange
        var user = NewUser(HashA);
        var oldToken = Read(NewIssuer().Issue(user).AccessToken);

        // Act
        user.PasswordHash = Hasher.Hash("MatKhauMoi2027");

        // Assert
        Assert.NotEqual(PasswordStamp.From(user.PasswordHash), oldToken.Claims.Single(c => c.Type == PasswordStamp.ClaimType).Value);
    }

    [Fact]
    public void HanTokenTinhTheoCauHinh()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 9, 22, 8, 0, 0, TimeSpan.Zero);

        // Act
        var issued = NewIssuer(new FixedClock(now)).Issue(NewUser(HashA));

        // Assert
        Assert.Equal(now.AddMinutes(120), issued.ExpiresAt);
        Assert.Equal(issued.ExpiresAt.UtcDateTime, Read(issued.AccessToken).ValidTo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void HanKhongDuong_NemLoiNgayLucCap(int expiryMinutes)
    {
        // Arrange — JwtSecurityToken bắt expires phải sau notBefore nên cấu hình sai lộ ngay lúc cấp,
        // không lọt token hết-hạn-ngay ra ngoài.
        var now = new DateTimeOffset(2026, 9, 22, 8, 0, 0, TimeSpan.Zero);
        var issuer = NewIssuer(new FixedClock(now), expiryMinutes);

        // Act
        void Act() => issuer.Issue(NewUser(HashA));

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void TokenKyBangKhoaKhac_KhongQuaDuocChuKy()
    {
        // Arrange
        var user = NewUser(HashA);
        var token = NewIssuer(key: TestKey).Issue(user).AccessToken;

        // Act
        var Act = () => Validate(token, "khoa-hoan-toan-khac-nhung-van-du-32-ky-tu!!");

        // Assert
        Assert.ThrowsAny<SecurityTokenException>(Act);
    }

    [Fact]
    public void TokenBiSua_MotKyTuCungRotChuKy()
    {
        // Arrange
        var user = NewUser(HashA);
        var token = NewIssuer().Issue(user).AccessToken;
        var tampered = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');

        // Act
        var Act = () => Validate(tampered, TestKey);

        // Assert
        Assert.ThrowsAny<Exception>(Act);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DauTuBanBamRong_AnToanHayNemLoiRangBuoc(string? hash)
    {
        // Arrange + Act
        var Act = () => PasswordStamp.From(hash!);

        // Assert — SHA-256 trên chuỗi rỗng vẫn ra dấu hợp lệ, null phải ném chứ không ra dấu bậy.
        if (hash is null)
            Assert.Throws<ArgumentNullException>(Act);
        else
            Assert.Matches("^[0-9a-f]{16}$", Act());
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

    private static JwtAccessTokenIssuer NewIssuer(TimeProvider? clock = null, int expiryMinutes = 120, string? key = null) => new(
        Options.Create(new JwtOptions
        {
            Issuer = "codeswitchlabel-test",
            Audience = "codeswitchlabel-test",
            Key = key ?? TestKey,
            ExpiryMinutes = expiryMinutes
        }),
        clock ?? TimeProvider.System);

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static ClaimsPrincipal Validate(string token, string key) =>
        new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "codeswitchlabel-test",
            ValidateAudience = true,
            ValidAudience = "codeswitchlabel-test",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero
        }, out _);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
