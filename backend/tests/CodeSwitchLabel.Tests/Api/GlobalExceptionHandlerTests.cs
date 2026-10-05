using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSwitchLabel.Tests.Api;

/// <summary>Một chỗ duy nhất đổi exception thành ProblemDetails — kiểm mã HTTP + mã lỗi bất biến.</summary>
[Trait("Category", "Unit")]
public sealed class GlobalExceptionHandlerTests
{
    private static (GlobalExceptionHandler Handler, DefaultHttpContext Context) Create(bool isDevelopment = false)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(isDevelopment ? "Development" : "Production");

        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, env.Object);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/scripts";
        context.Response.Body = new MemoryStream();
        return (handler, context);
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData(404, "script_not_found")]
    [InlineData(409, "duplicate_content")]
    [InlineData(422, "qc_failed")]
    public async Task LoiNghiepVu_TraDungStatusVaMa(int status, string code)
    {
        // Arrange
        var (handler, context) = Create();
        AppException ex = status switch
        {
            404 => NotFoundException.Script("s_111000001"),
            409 => ConflictException.DuplicateContent(),
            _ => new UnprocessableException(code, "Không đạt chất lượng."),
        };

        // Act
        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Contains($"\"code\":\"{code}\"", ReadBody(context));
    }

    [Fact]
    public async Task LoiLa_Production_GiauChiTietVaTra500()
    {
        // Arrange
        var (handler, context) = Create(isDevelopment: false);

        // Act
        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("Bug nội bộ"), CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        var body = ReadBody(context);
        Assert.Contains("internal_error", body);
        Assert.DoesNotContain("Bug nội bộ", body);
    }

    [Fact]
    public async Task LoiLa_Development_LoStackTrace()
    {
        // Arrange
        var (handler, context) = Create(isDevelopment: true);

        // Act
        await handler.TryHandleAsync(context, new InvalidOperationException("Bug nội bộ"), CancellationToken.None);

        // Assert
        Assert.Contains("Bug nội bộ", ReadBody(context));
    }
}
