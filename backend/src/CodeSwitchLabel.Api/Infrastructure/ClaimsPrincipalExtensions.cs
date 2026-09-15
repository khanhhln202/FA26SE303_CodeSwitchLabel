using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CodeSwitchLabel.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Lấy id người đang đăng nhập từ claim "sub" của JWT.
    ///
    /// Đọc được claim tên "sub" là nhờ đã tắt MapInboundClaims trong Program.cs.
    /// Nếu để mặc định, ASP.NET đổi "sub" thành một URI dài của schema SOAP cũ,
    /// và dòng dưới đây sẽ trả về null mà không báo gì.
    /// </summary>
    public static long GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        // Đến được đây mà không có claim nghĩa là [Authorize] đã bị bỏ sót ở đâu đó —
        // đó là bug cấu hình, không phải ca nghiệp vụ, nên ném lỗi ngay.
        return long.TryParse(raw, out var id)
            ? id
            : throw new InvalidOperationException(
                "Token hợp lệ nhưng thiếu claim định danh người dùng.");
    }
}
