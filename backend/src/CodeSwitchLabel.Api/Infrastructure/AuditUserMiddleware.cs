using CodeSwitchLabel.Repositories.Persistence;

namespace CodeSwitchLabel.Api.Infrastructure;

/// <summary>
/// Cho DbContext biết ai đang thao tác, để trigger fn_audit ghi đúng người vào audit_log.
/// Trigger đọc biến phiên app.user_id; DbContext đặt biến đó ngay trước mỗi lần lưu.
///
/// Phải chạy SAU UseAuthentication, vì trước đó request chưa có danh tính.
/// </summary>
public class AuditUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CodeSwitchLabelDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            db.AuditUserId = context.User.GetUserId();
        }

        await next(context);
    }
}
