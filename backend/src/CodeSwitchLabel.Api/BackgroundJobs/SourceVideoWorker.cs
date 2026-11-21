using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Options;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Api.BackgroundJobs;

/// <summary>
/// Tải YouTube và phiên âm chạy nền: request chỉ ghi một hàng rồi trả về ngay, nên
/// tải video dài không bị cắt theo timeout HTTP. Job nằm trong database, API khởi động
/// lại vẫn làm tiếp; lỗi được ghi vào chính hàng đó để Admin thấy và thử lại.
/// </summary>
public sealed class SourceVideoWorker(
    IServiceScopeFactory scopes,
    IOptions<SourceCorpusOptions> options,
    ILogger<SourceVideoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Tiến trình tải video nguồn đã khởi động (yt-dlp: {YtDlp}, JS runtime: {Js}).",
            options.Value.YtDlpPath, options.Value.JsRuntimes);

        var idleDelay = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;

            try
            {
                // Mỗi lượt một phạm vi riêng: job chạy lâu, không giữ DbContext của request nào.
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ISourceVideoService>();
                worked = await service.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Để lọt ra khỏi ExecuteAsync là cả tiến trình nền dừng hẳn, lặng lẽ.
                logger.LogError(ex, "Lượt xử lý video nguồn hỏng. Sẽ thử lại lượt sau.");
            }

            if (worked) continue;

            try
            {
                await Task.Delay(idleDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Tiến trình tải video nguồn đã dừng.");
    }
}
