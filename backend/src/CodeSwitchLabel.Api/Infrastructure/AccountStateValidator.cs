using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CodeSwitchLabel.Api.Infrastructure;

/// <summary>
/// Chạy sau khi chữ ký token đã hợp lệ, ở MỌI request đã đăng nhập.
///
/// JWT tự nó không biết tài khoản vừa bị khoá, vừa bị đổi vai hay vừa đổi mật khẩu: token cấp lúc 8 giờ sáng
/// vẫn ghi đúng những gì của 8 giờ sáng cho tới khi hết hạn. Đối chiếu với database ở mỗi request thì ba việc
/// đó có hiệu lực ngay. Cái giá là một câu truy vấn theo khoá chính, chỉ đọc ba cột — không đáng kể ở quy mô đồ án.
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
            return;
        }

        // Mật khẩu đã đổi hoặc được cấp lại sau khi token này ra đời, nên dấu trong token là dấu của mật khẩu cũ.
        // Token cấp trước khi có dấu cũng rơi vào đây: mọi người đăng nhập lại một lần là xong.
        if (principal.FindFirstValue(PasswordStamp.ClaimType) != PasswordStamp.From(state.PasswordHash))
        {
            context.Fail("Mật khẩu vừa được đổi, cần đăng nhập lại.");
        }
    }
}
