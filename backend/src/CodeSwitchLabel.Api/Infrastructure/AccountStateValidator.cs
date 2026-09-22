using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CodeSwitchLabel.Api.Infrastructure;

/// <summary>
/// Chạy sau khi chữ ký token đã hợp lệ, ở MỌI request đã đăng nhập.
///
/// JWT tự nó không biết tài khoản vừa bị khoá hay vừa bị đổi vai: token cấp lúc 8 giờ sáng vẫn ghi vai cũ
/// cho tới khi hết hạn. Đối chiếu với database ở mỗi request thì khoá tài khoản và đổi vai có hiệu lực ngay.
/// Cái giá là một câu truy vấn theo khoá chính, chỉ đọc hai cột — không đáng kể ở quy mô đồ án.
/// </summary>
public static class AccountStateValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;

        if (principal is null || !long.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            context.Fail("Token không có mã người dùng.");
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var state = await users.GetAuthStateAsync(userId, context.HttpContext.RequestAborted);

        if (state is null || state.Status != UserStatus.Active)
        {
            context.Fail("Tài khoản đã bị khoá hoặc không còn tồn tại.");
            return;
        }

        // Vai trong token khác vai hiện tại: bắt đăng nhập lại để nhận token mang vai mới.
        if (state.Role.ToString() != principal.FindFirstValue(ClaimTypes.Role))
        {
            context.Fail("Vai của tài khoản vừa được đổi, cần đăng nhập lại để nhận quyền mới.");
        }
    }
}
