using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Services.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CodeSwitchLabel.Services.Common;

/// <summary>
/// "Dấu" của mật khẩu, gắn vào token lúc cấp. Ở mỗi request, backend tính lại dấu từ mật khẩu hiện tại trong
/// database rồi so với dấu trong token. Đổi hay cấp lại mật khẩu là dấu đổi theo, nên mọi token cấp trước đó
/// đều bị từ chối — kể cả token đang nằm trong tay người lạ.
///
/// Dấu là 16 ký tự đầu của SHA-256 tính trên chuỗi bcrypt: không phải mật khẩu, cũng không phải bản băm.
/// Không lần ngược được ra mật khẩu, kể cả bằng cách đoán, vì muốn thử một mật khẩu phải có muối ngẫu nhiên
/// nằm trong chuỗi bcrypt — thứ chỉ database giữ. Dấu cũng không cần giữ bí mật: ai cầm token đều đọc được
/// nội dung, nhưng không ai sửa được vì token có chữ ký.
/// </summary>
public static class PasswordStamp
{
    public const string ClaimType = "pwd_stamp";

    public static string From(string passwordHash) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)).AsSpan(0, 8));
}

public record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Cấp access token. Dùng chung cho đăng nhập và đổi mật khẩu: đổi mật khẩu xong thì token cũ mang dấu
/// của mật khẩu cũ, phải cấp token mới thì người dùng mới ở lại trang được.
/// </summary>
public interface IAccessTokenIssuer
{
    /// <param name="user">Phải kèm <see cref="AppUser.Role"/>.</param>
    IssuedToken Issue(AppUser user);
}

public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JwtOptions _jwt = options.Value;

    public IssuedToken Issue(AppUser user)
    {
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(_jwt.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.FullName),

            // Một vai duy nhất, đúng như lược đồ. Nhờ vậy [Authorize(Roles = "...")] chạy bình thường.
            new(ClaimTypes.Role, user.Role.RoleName.ToString()),

            new(PasswordStamp.ClaimType, PasswordStamp.From(user.PasswordHash))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
