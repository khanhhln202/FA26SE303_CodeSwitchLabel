using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Infrastructure;

/// <summary>
/// C3: [Authorize(Roles=...)] mặc định trả 403 rỗng khi sai vai — frontend không
/// biết hiện gì ("generic failure"). Handler này trả ProblemDetails có mã để
/// báo rõ "không có quyền" thay vì body rỗng.
/// </summary>
public class JsonAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && !context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Tài khoản của bạn không có quyền dùng chức năng này.",
                Type = "https://codeswitchlabel.local/errors/forbidden",
                Instance = $"{context.Request.Method} {context.Request.Path}",
                Extensions =
                {
                    ["code"] = "forbidden",
                    ["traceId"] = context.TraceIdentifier
                }
            });
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
