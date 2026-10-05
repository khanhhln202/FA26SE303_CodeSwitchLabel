using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CodeSwitchLabel.Api.Infrastructure;

namespace CodeSwitchLabel.Tests.Api;

/// <summary>Đọc id người đăng nhập từ claim "sub" — thiếu là bug cấu hình [Authorize], phải ném ngay.</summary>
[Trait("Category", "Unit")]
public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_VoiClaimSub_TraDungId()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, "42")], "Test"));

        // Act
        var id = principal.GetUserId();

        // Assert
        Assert.Equal(42, id);
    }

    [Fact]
    public void GetUserId_VoiNameIdentifier_KhiKhongCoSub_VanDocDuoc()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "7")], "Test"));

        // Act
        var id = principal.GetUserId();

        // Assert
        Assert.Equal(7, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khong-phai-so")]
    public void GetUserId_ThieuHoacSaiClaim_NemInvalidOperation(string? raw)
    {
        // Arrange
        var claims = raw is null ? [] : new[] { new Claim(JwtRegisteredClaimNames.Sub, raw) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        void Act() => principal.GetUserId();

        // Assert
        Assert.Throws<InvalidOperationException>(Act);
    }
}
