using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Services.Implementations;

internal static class RecordingMapper
{
    public static RecordingDto ToDto(this Recording r) =>
        new(r.RecordingId, r.ScriptId, r.SentenceVariant, r.SpeakerId, r.TaskId,
            r.Status, r.AudioFormat, r.DurationSec, r.RecordedAt);
}

public class RecordingService(
    IRecordingRepository recordings,
    IScriptRepository scripts,
    IObjectStorage storage,
    IAudioProcessor audio,
    ISystemConfigService config,
    ITaskProgressTracker taskTracker,
    IOptions<AudioOptions> audioOptions,
    TimeProvider clock,
    ILogger<RecordingService> logger) : IRecordingService
{
    private readonly AudioOptions _audio = audioOptions.Value;

    public async Task<UploadRecordingResult> UploadAsync(
        UploadRecordingCommand command, CancellationToken ct = default)
    {
        EnsureSizeWithinLimit(command.Length);

        // Kiểm quyền và trùng lặp TRƯỚC khi đụng tới file — không bắt ffmpeg làm việc
        // cho một request đằng nào cũng bị từ chối.
        var taskId = await EnsureCanRecordAsync(command, ct);

        var workDir = Path.Combine(Path.GetTempPath(), "csl-audio");
        Directory.CreateDirectory(workDir);

        var fileId = Guid.NewGuid().ToString("N");

        // Tên file gốc và file WAV đầu ra PHẢI khác nhau: client gửi sẵn WAV thì cả hai cùng đuôi,
        // mà ffmpeg từ chối ghi đè chính file đang đọc.
        var inputPath = Path.Combine(workDir, fileId + "-source" + SafeExtension(command.FileName));
        var wavPath = Path.Combine(workDir, fileId + "-16k.wav");

        try
        {
            await using (var file = File.Create(inputPath))
            {
                await command.Audio.CopyToAsync(file, ct);
            }

            var probe = await ConvertAndMeasureAsync(inputPath, wavPath, ct);

            // TEAM_001: QC giờ đo thêm khoảng lặng/âm lượng và ghi lại toàn bộ kết quả vào qc_metrics.
            var (issues, qcReport) = await RunQualityChecksAsync(probe, wavPath, ct);
            var now = clock.GetUtcNow();

            var take = await NextTakeAsync(command, ct);

            // Mã bản ghi do database sinh: r_cs_/r_vi_ + 9 chữ số của cặp câu, thu lại thì thêm _tN.
            var recordingId = await recordings.GenerateIdAsync(
                command.ScriptId, command.SentenceVariant, take, ct);

            // Đặt tên file theo đúng mã bản ghi: nhìn file trong kho là biết của câu nào, lần thu thứ mấy.
            var key = $"recordings/{now:yyyy}/{now:MM}/{recordingId}.wav";

            await storage.UploadAsync(key, wavPath, "audio/wav", ct);

            var recording = new Recording
            {
                RecordingId = recordingId,
                SentenceVariant = command.SentenceVariant,
                ScriptId = command.ScriptId,
                SpeakerId = command.SpeakerId,
                TaskId = taskId,
                CloudLink = storage.GetObjectUrl(key),
                AudioFormat = "wav",

                // Trượt kiểm tra vẫn lưu cả hàng lẫn file, đúng trạng thái qc_failed của lược đồ,
                // nhờ vậy vẫn đếm được mỗi người trượt bao nhiêu lần.
                Status = issues.Count == 0 ? RecordingStatus.PendingReview : RecordingStatus.QcFailed,

                DurationSec = probe.DurationSec,
                QcMetrics = RecordingQcJson.Serialize(qcReport),
                RecordedAt = now
            };

            try
            {
                // Lưu bản ghi và cập nhật tiến độ task trong cùng một transaction.
                await using var transaction = await recordings.BeginTransactionAsync(ct);

                recordings.Add(recording);
                await recordings.SaveChangesAsync(ct);

                if (taskId.HasValue)
                {
                    await taskTracker.OnRecordingSubmittedAsync(taskId.Value, ct);
                }

                await transaction.CommitAsync(ct);
            }
            catch
            {
                // Ghi database thất bại thì cố dọn file vừa đẩy lên, rồi ném lỗi tiếp.
                await TryDeleteOrphanAsync(key);
                throw;
            }

            return new UploadRecordingResult(recording.ToDto(), issues.Count == 0, issues, take, qcReport);
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(wavPath);
        }
    }

    public async Task<PagedResult<RecordingDto>> SearchAsync(
        RecordingSearchRequest request, long? ownerOnlyUserId, CancellationToken ct = default)
    {
        // Speaker chỉ thấy bản của chính mình, bất kể họ lọc theo speakerId nào.
        var speakerId = ownerOnlyUserId ?? request.SpeakerId;

        var (items, total) = await recordings.SearchAsync(
            speakerId, request.ScriptId, request.Status, request.Page, request.PageSize, ct);

        return new PagedResult<RecordingDto>(
            [.. items.Select(r => r.ToDto())], request.Page, request.PageSize, total);
    }

    public async Task<RecordingDto> GetAsync(
        string recordingId, long? ownerOnlyUserId, CancellationToken ct = default) =>
        (await LoadAccessibleAsync(recordingId, ownerOnlyUserId, ct)).ToDto();

    public async Task<AudioUrlDto> GetAudioUrlAsync(
        string recordingId, long? ownerOnlyUserId, CancellationToken ct = default)
    {
        var recording = await LoadAccessibleAsync(recordingId, ownerOnlyUserId, ct);

        var key = storage.GetObjectKey(recording.CloudLink)
                  ?? throw new UnprocessableException(
                      "external_recording",
                      $"Bản ghi {recordingId} trỏ tới một kho lưu trữ khác, hệ thống không cấp link nghe được.");

        var (url, expiresAt) = await storage.GetDownloadUrlAsync(key, ct);

        return new AudioUrlDto(url, expiresAt);
    }

    // ----------------------------------------------------------------- nội bộ

    private void EnsureSizeWithinLimit(long length)
    {
        if (length <= 0)
        {
            throw new UnprocessableException("empty_audio", "File âm thanh rỗng.");
        }

        if (length > _audio.MaxUploadMegabytes * 1024L * 1024L)
        {
            throw new UnprocessableException(
                "audio_too_large", $"File vượt quá giới hạn {_audio.MaxUploadMegabytes} MB.");
        }
    }

    /// <returns>Task mà bản ghi thuộc về, hoặc null nếu thu ngoài task.</returns>
    private async Task<long?> EnsureCanRecordAsync(UploadRecordingCommand command, CancellationToken ct)
    {
        var script = await scripts.GetAsync(command.ScriptId, ct)
                     ?? throw NotFoundException.Script(command.ScriptId);

        if (script.Status != ScriptStatus.Validated)
        {
            throw new ConflictException(
                "script_not_recordable",
                $"Cặp câu {script.ScriptId} đang ở trạng thái {script.Status}, chỉ thu âm được câu đã duyệt nội dung.");
        }

        // Luật cốt lõi của lược đồ: một cặp câu chỉ một người đọc, để hai bản cs và vi
        // là cùng một giọng, cùng một buổi thu. Database cũng có trigger chặn.
        var owner = await recordings.GetOwnerSpeakerIdAsync(command.ScriptId, ct);

        if (owner.HasValue && owner.Value != command.SpeakerId)
        {
            throw new ConflictException(
                "script_owned_by_other_speaker",
                $"Cặp câu {command.ScriptId} đã do người đọc khác thu. Mỗi cặp câu chỉ một người đọc.");
        }

        if (command.TaskId.HasValue &&
            !await recordings.IsRecordableInTaskAsync(command.TaskId.Value, command.SpeakerId, command.ScriptId, ct))
        {
            throw new ForbiddenException(
                "task_not_recordable",
                $"Task #{command.TaskId} không phải task thu âm đang giao cho bạn, hoặc không chứa cặp câu này.");
        }

        if (await recordings.HasActiveRecordingAsync(command.ScriptId, command.SentenceVariant, ct))
        {
            throw new ConflictException(
                "recording_already_exists",
                $"Bạn đã có bản {command.SentenceVariant} đang chờ duyệt hoặc đã được duyệt cho cặp câu {command.ScriptId}.");
        }

        // Không gửi taskId nhưng cặp câu lại đang nằm trong task của chính người này thì tự gắn vào task.
        return command.TaskId
               ?? await recordings.FindOwnTaskContainingScriptAsync(command.SpeakerId, command.ScriptId, ct);
    }

    private async Task<int> NextTakeAsync(UploadRecordingCommand command, CancellationToken ct)
    {
        var take = await recordings.CountTakesAsync(command.ScriptId, command.SentenceVariant, ct) + 1;
        var maxTake = await config.GetIntAsync(ConfigKeys.RecordingMaxTake, 99, ct);

        if (take > maxTake)
        {
            throw new ConflictException(
                "too_many_takes",
                $"Cặp câu {command.ScriptId} đã thu {take - 1} lần cho biến thể này, vượt mức {maxTake} lần.");
        }

        return take;
    }

    private async Task<AudioProbeResult> ConvertAndMeasureAsync(
        string inputPath, string wavPath, CancellationToken ct)
    {
        AudioProbeResult probe;

        try
        {
            await audio.ConvertToWavAsync(inputPath, wavPath, ct);

            // Đo trên file WAV SAU khi chuyển, không đo file gốc: trình duyệt ghi WebM theo kiểu
            // phát trực tiếp nên phần đầu file thường không có thời lượng.
            probe = await audio.ProbeAsync(wavPath, ct);
        }
        catch (AudioProcessingException ex)
        {
            throw new UnprocessableException("invalid_audio", $"Không xử lý được file âm thanh: {ex.Message}");
        }

        if (probe.DurationSec <= 0)
        {
            // Không lưu được dưới dạng qc_failed: cột duration_sec có ràng buộc lớn hơn 0.
            throw new UnprocessableException("invalid_audio", "File âm thanh không có nội dung.");
        }

        return probe;
    }

    /// <summary>
    /// Chạy trọn bộ kiểm tra tự động: thời lượng, khoảng lặng đầu/cuối, âm lượng.
    /// Trả cả danh sách lỗi (để chốt qc_failed) lẫn báo cáo đầy đủ (để ghi vào qc_metrics).
    /// Mọi ngưỡng đọc từ system_config; thiếu hàng thì dùng mặc định ghi kèm.
    /// </summary>
    private async Task<(IReadOnlyList<QcIssueDto> Issues, RecordingQcReport Report)> RunQualityChecksAsync(
        AudioProbeResult probe, string wavPath, CancellationToken ct)
    {
        var min = await config.GetDecimalAsync(ConfigKeys.RecordingMinDurationSec, 1m, ct);
        var max = await config.GetDecimalAsync(ConfigKeys.RecordingMaxDurationSec, 30m, ct);
        var maxLeading = await config.GetDecimalAsync(ConfigKeys.RecordingMaxLeadingSilenceSec, 1m, ct);
        var maxTrailing = await config.GetDecimalAsync(ConfigKeys.RecordingMaxTrailingSilenceSec, 1m, ct);
        var silenceNoiseDb = await config.GetDecimalAsync(ConfigKeys.RecordingSilenceNoiseDb, -35m, ct);
        var silenceMinSec = await config.GetDecimalAsync(ConfigKeys.RecordingSilenceMinDurationSec, 0.5m, ct);

        var signal = await audio.AnalyzeSignalAsync(wavPath, probe.DurationSec, silenceNoiseDb, silenceMinSec, ct);
        var issues = RecordingQcEvaluator.Evaluate(probe.DurationSec, signal, min, max, maxLeading, maxTrailing);

        var report = new RecordingQcReport(
            issues.Count == 0,
            probe.DurationSec,
            signal.LeadingSilenceSec,
            signal.TrailingSilenceSec,
            signal.MeanVolumeDb,
            signal.MaxVolumeDb,
            signal.ClippingSuspected,
            issues);

        return (issues, report);
    }

    private async Task<Recording> LoadAccessibleAsync(
        string recordingId, long? ownerOnlyUserId, CancellationToken ct)
    {
        var recording = await recordings.GetAsync(recordingId, ct);

        // Speaker hỏi bản ghi của người khác thì trả 404 như thể không tồn tại, không trả 403 —
        // trả 403 là vô tình xác nhận rằng mã đó có thật.
        if (recording is null || (ownerOnlyUserId.HasValue && recording.SpeakerId != ownerOnlyUserId.Value))
        {
            throw new NotFoundException("recording_not_found", $"Không tìm thấy bản ghi {recordingId}.");
        }

        return recording;
    }

    /// <summary>
    /// Chỉ lấy phần đuôi file để ffmpeg đoán định dạng. Tên file do client gửi KHÔNG BAO GIỜ
    /// được dùng làm đường dẫn — chặn kiểu tấn công ../../ ghi đè file hệ thống.
    /// </summary>
    private static string SafeExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);

        return ext is { Length: > 1 and <= 6 } && ext.Skip(1).All(char.IsLetterOrDigit)
            ? ext.ToLowerInvariant()
            : ".bin";
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // File tạm đang bị khoá — hệ điều hành sẽ dọn thư mục tạm sau.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private async Task TryDeleteOrphanAsync(string key)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không dọn được file mồ côi {Key} trong kho lưu trữ", key);
        }
    }
}
