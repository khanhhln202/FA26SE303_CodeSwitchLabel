using CodeSwitchLabel.Services.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Infrastructure;

/// <summary>
/// Một chỗ duy nhất đổi exception thành phản hồi HTTP.
///
/// Nhờ nó mà controller không có khối try/catch nào, và tầng Service
/// không cần biết gì về HTTP — Service ném lỗi nghiệp vụ, chỗ này dịch sang mã trạng thái.
///
/// Định dạng lỗi theo ProblemDetails (RFC 7807) — chuẩn sẵn có của .NET,
/// nên Swagger và các thư viện phía client hiểu ngay.
/// </summary>
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            AppException app => HandleAppException(app),
            _ => MapTriggerException(exception) ?? HandleUnexpected(exception)
        };

        problem.Instance = $"{context.Request.Method} {context.Request.Path}";
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, ct);

        return true;
    }

    /// <summary>
    /// Lỗi nghiệp vụ là chuyện bình thường, không phải sự cố — ghi log mức
    /// Information để khỏi làm nhiễu cảnh báo thật.
    /// </summary>
    private ProblemDetails HandleAppException(AppException exception)
    {
        logger.LogInformation("Lỗi nghiệp vụ {Code}: {Message}", exception.Code, exception.Message);

        return new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.Message,
            Type = $"https://codeswitchlabel.local/errors/{exception.Code}",
            Extensions = { ["code"] = exception.Code }
        };
    }

    /// <summary>
    /// C3: trigger PostgreSQL là lưới chắn cuối (race với kiểm tra ở service,
    /// hoặc ghi thẳng bằng SQL). Dịch lỗi thô thành mã nghiệp vụ để frontend
    /// hiện được thay vì 500 internal_error chung chung.
    /// </summary>
    private static ProblemDetails? MapTriggerException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message ?? string.Empty;

            if (message.Contains("target exceeded", StringComparison.OrdinalIgnoreCase))
            {
                return BusinessProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "task_target_exceeds_campaign",
                    "Chỉ tiêu task vượt phần còn lại của chiến dịch.");
            }

            if (message.Contains("must be within campaign", StringComparison.OrdinalIgnoreCase))
            {
                return BusinessProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "task_deadline_outside_campaign",
                    "Hạn của task nằm ngoài khoảng ngày của chiến dịch.");
            }

            if (message.Contains("no assigned task manager", StringComparison.OrdinalIgnoreCase)
                || message.Contains("must be the assigned task manager", StringComparison.OrdinalIgnoreCase))
            {
                return BusinessProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "campaign_not_assigned_to_manager",
                    "Chiến dịch chưa được giao cho bạn nên chưa nhận task.");
            }

            if (message.Contains("cannot be validated", StringComparison.OrdinalIgnoreCase))
            {
                return BusinessProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "reviewer_domain_required",
                    "Chỉ người được phân đúng chủ đề mới duyệt được cặp câu này sang trạng thái đã duyệt.");
            }

            if (message.Contains("self-review blocked", StringComparison.OrdinalIgnoreCase))
            {
                return BusinessProblem(
                    StatusCodes.Status403Forbidden,
                    "self_review_forbidden",
                    "Bạn không được duyệt bản ghi do chính mình thu.");
            }
        }

        return null;
    }

    private static ProblemDetails BusinessProblem(int status, string code, string title) =>
        new()
        {
            Status = status,
            Title = title,
            Type = $"https://codeswitchlabel.local/errors/{code}",
            Extensions = { ["code"] = code }
        };

    private ProblemDetails HandleUnexpected(Exception exception)
    {
        logger.LogError(exception, "Lỗi không lường trước");

        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Đã có lỗi xảy ra phía máy chủ.",
            Type = "https://codeswitchlabel.local/errors/internal_error",
            Extensions =
            {
                ["code"] = "internal_error",

                // Chi tiết lỗi CHỈ lộ ra khi chạy dev. Trả stack trace cho client
                // thật là tự tiết lộ cấu trúc bên trong hệ thống.
                ["detail"] = environment.IsDevelopment() ? exception.ToString() : null
            }
        };
    }
}
