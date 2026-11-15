using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;

namespace CodeSwitchLabel.Api.BackgroundJobs;

/// <summary>
/// Tự gọi máy duyệt cho những bản ghi đang ở thế hai Reviewer người duyệt lệch nhau, thay
/// vì chờ ai đó nhớ ra mà bấm nút.
///
/// <para>
/// <b>Vì sao là tiến trình nền quét định kỳ, không phải "bắn rồi quên" ngay lúc bỏ phiếu.</b>
/// Ba lý do, mỗi lý do đủ để loại cách kia:
/// </para>
/// <list type="number">
/// <item><b>Mất việc.</b> Tác vụ bắn kèm request sẽ chết cùng request nếu API khởi động
/// lại hay nó ném lỗi — bản ghi nằm lại vĩnh viễn, không dấu vết, không ai thử lại. Ở đây
/// việc nằm trong database: điều kiện hoà 1–1 CHÍNH LÀ hàng đợi, nên khởi động lại xong
/// quét tiếp là gặp lại.</item>
/// <item><b>Không có van chặn.</b> Ba người bỏ phiếu cùng lúc là ba lần nhận dạng song
/// song, mỗi lần đòi khoảng 1 GB trên máy chủ chỉ còn hơn 2 GB trống. Ở đây xử lý TUẦN TỰ
/// nên số lần chạy đồng thời luôn là một.</item>
/// <item><b>Vòng đời DbContext.</b> Nó gắn với request; request kết thúc là nó bị huỷ, tác
/// vụ bắn kèm đang chạy sẽ gặp đối tượng đã huỷ. Ở đây mỗi lượt quét mở phạm vi riêng.</item>
/// </list>
///
/// <para>
/// KHÔNG cần bảng hàng đợi, và cố ý không thêm: <see cref="IAiReviewService.GetPendingAsync"/>
/// đã tìm đúng tập cần xử lý từ chính dữ liệu duyệt. Thêm bảng là thêm một nguồn sự thật
/// thứ hai có thể lệch với nguồn thứ nhất.
/// </para>
/// </summary>
public sealed class AiReviewWorker(
    IServiceScopeFactory scopes,
    ILogger<AiReviewWorker> logger) : BackgroundService
{
    private readonly AiReviewRetryBudget _budget = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Tiến trình máy duyệt tự động đã khởi động. Bật/tắt bằng {Key} trong cấu hình hệ thống.",
            ConfigKeys.AiReviewAutoEnabled);

        var delay = AiReviewAutoSettings.DefaultInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                delay = await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // BackgroundService mà để lọt lỗi ra khỏi ExecuteAsync là nó DỪNG HẲN, lặng
                // lẽ, cho tới lần khởi động sau. Nuốt ở đây để một lượt hỏng không giết cả
                // tính năng.
                logger.LogError(ex, "Lượt quét của máy duyệt tự động hỏng. Sẽ thử lại lượt sau.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Tiến trình máy duyệt tự động đã dừng.");
    }

    /// <summary>Một lượt quét. Trả về khoảng nghỉ cho lượt kế tiếp, đọc lại từ cấu hình mỗi lần.</summary>
    private async Task<TimeSpan> TickAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();

        var config = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();
        var settings = AiReviewAutoSettings.From(await config.GetAllAsync(ct));

        // Đọc lại cấu hình MỖI lượt chứ không cache: bật tắt được lúc đang chạy, không phải
        // khởi động lại container — thứ cần có khi máy chủ đang gồng.
        if (!settings.Enabled) return settings.Interval;

        var aiReview = scope.ServiceProvider.GetRequiredService<IAiReviewService>();

        // Lấy rộng hơn mức xử lý một chút để còn bỏ qua được những bản đã hỏng quá số lần.
        var pending = await aiReview.GetPendingAsync(settings.BatchSize * 4, ct);
        var done = 0;

        foreach (var item in pending)
        {
            if (done >= settings.BatchSize || ct.IsCancellationRequested) break;
            if (!_budget.CanTry(item.RecordingId)) continue;

            try
            {
                var result = await aiReview.AnalyzeAsync(item.RecordingId, ct);
                _budget.RecordSuccess(item.RecordingId);
                done++;

                logger.LogInformation(
                    "Máy duyệt tự động: {RecordingId} -> {Verdict}, WER {Wer}.",
                    result.RecordingId, result.Verdict, result.EffectiveWer);
            }
            catch (AppException ex)
            {
                // Lỗi nghiệp vụ của MỘT bản không được làm đổ cả lượt: có người vừa bỏ phiếu
                // xong làm bản ghi hết hoà, hoặc ASR trục trặc. Đếm rồi đi tiếp.
                var attempts = _budget.RecordFailure(item.RecordingId);

                logger.Log(
                    _budget.CanTry(item.RecordingId) ? LogLevel.Warning : LogLevel.Error,
                    ex,
                    "Máy duyệt tự động bỏ qua {RecordingId} (lần {Attempts}): {Code}.{GiveUp}",
                    item.RecordingId, attempts, ex.Code,
                    _budget.CanTry(item.RecordingId)
                        ? string.Empty
                        : " Đã quá số lần cho phép, sẽ không thử lại — cần người xử lý tay.");
            }
        }

        return settings.Interval;
    }
}
