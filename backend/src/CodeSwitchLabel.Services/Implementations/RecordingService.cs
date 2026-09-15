using System.Globalization;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Services.Implementations;

internal static class RecordingMapper
{
    public static RecordingDto ToDto(this Recording r) =>
        new(r.RecordingId, r.ScriptId, r.SpeakerId, r.TaskId,
            r.Status, r.AudioFormat, r.DurationSec, r.RecordedAt);
}

public class RecordingService(
    IRecordingRepository recordings,
    IScriptRepository scripts,
    IObjectStorage storage,
    IAudioProcessor audio,
    ISystemConfigService config,
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
        await EnsureCanRecordAsync(command, ct);

        var workDir = Path.Combine(Path.GetTempPath(), "csl-audio");
        Directory.CreateDirectory(workDir);

        var fileId = Guid.NewGuid().ToString("N");
        var inputPath = Path.Combine(workDir, fileId + SafeExtension(command.FileName));
        var wavPath = Path.Combine(workDir, fileId + ".wav");

        try
        {
            await using (var file = File.Create(inputPath))
            {
                await command.Audio.CopyToAsync(file, ct);
            }

            var probe = await ConvertAndMeasureAsync(inputPath, wavPath, ct);
            var issues = await RunQualityChecksAsync(probe, ct);
            var now = clock.GetUtcNow();

            // Khoá file dùng GUID chứ không dùng recording_id, vì id chỉ có SAU khi INSERT
            // mà file thì đẩy lên TRƯỚC. Thứ tự này có chủ ý: sập giữa chừng chỉ để lại
            // một file mồ côi — người dùng không thấy, dọn được — thay vì một hàng dữ liệu
            // trỏ tới file không tồn tại.
            var key = $"recordings/{now:yyyy}/{now:MM}/{fileId}.wav";

            await storage.UploadAsync(key, wavPath, "audio/wav", ct);

            var recording = new Recording
            {
                ScriptId = command.ScriptId,
                SpeakerId = command.SpeakerId,
                TaskId = command.TaskId,
                S3Key = key,
                AudioFormat = "wav",

                // Trượt kiểm tra vẫn lưu cả hàng lẫn file, đúng trạng thái qc_failed có sẵn trong ERD.
                // Nhờ vậy vẫn đếm được mỗi người trượt bao nhiêu lần.
                Status = issues.Count == 0 ? RecordingStatus.PendingReview : RecordingStatus.QcFailed,

                DurationSec = probe.DurationSec,
                RecordedAt = now
            };

            try
            {
                recordings.Add(recording);
                await recordings.SaveChangesAsync(ct);
            }
            catch
            {
                // Ghi database thất bại thì cố dọn file vừa đẩy lên, rồi ném lỗi tiếp.
                await TryDeleteOrphanAsync(key);
                throw;
            }

            return new UploadRecordingResult(recording.ToDto(), issues.Count == 0, issues);
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(wavPath);
        }
    }

    public async Task<PagedResult<RecordingDto>> GetMineAsync(
        long speakerId, RecordingSearchRequest request, CancellationToken ct = default)
    {
        var (items, total) = await recordings.SearchForSpeakerAsync(
            speakerId, request.Status, request.Page, request.PageSize, ct);

        return new PagedResult<RecordingDto>(
            [.. items.Select(r => r.ToDto())], request.Page, request.PageSize, total);
    }

    public async Task<RecordingDto> GetAsync(
        long recordingId, long? ownerOnlyUserId, CancellationToken ct = default) =>
        (await LoadAccessibleAsync(recordingId, ownerOnlyUserId, ct)).ToDto();

    public async Task<AudioUrlDto> GetAudioUrlAsync(
        long recordingId, long? ownerOnlyUserId, CancellationToken ct = default)
    {
        var recording = await LoadAccessibleAsync(recordingId, ownerOnlyUserId, ct);
        var (url, expiresAt) = await storage.GetDownloadUrlAsync(recording.S3Key, ct);

        return new AudioUrlDto(url, expiresAt);
    }

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

    private async Task EnsureCanRecordAsync(UploadRecordingCommand command, CancellationToken ct)
    {
        var script = await scripts.GetAsync(command.ScriptId, ct)
                     ?? throw NotFoundException.Script(command.ScriptId);

        if (script.Status != ScriptStatus.Validated)
        {
            throw new ConflictException(
                "script_not_recordable",
                $"Script #{script.ScriptId} đang ở trạng thái {script.Status}, chỉ thu âm được script đã duyệt.");
        }

        if (command.TaskId.HasValue &&
            !await recordings.IsRecordableInTaskAsync(command.TaskId.Value, command.SpeakerId, command.ScriptId, ct))
        {
            throw new ForbiddenException(
                "task_not_recordable",
                $"Task #{command.TaskId} không phải task thu âm đang giao cho bạn, hoặc không chứa script này.");
        }

        // Chặn ở tầng Service vì ERD không có ràng buộc duy nhất cho cặp (script, speaker).
        // GIỚI HẠN ĐÃ BIẾT: hai request gửi đúng cùng một lúc vẫn có thể cùng lọt qua bước này,
        // vì database không có gì chặn lại. Muốn hết hẳn thì phải thêm ràng buộc vào lược đồ.
        if (await recordings.HasActiveRecordingAsync(command.ScriptId, command.SpeakerId, ct))
        {
            throw new ConflictException(
                "recording_already_exists",
                $"Bạn đã có bản ghi đang chờ duyệt hoặc đã được duyệt cho script #{command.ScriptId}.");
        }
    }

    private async Task<AudioProbeResult> ConvertAndMeasureAsync(
        string inputPath, string wavPath, CancellationToken ct)
    {
        AudioProbeResult probe;

        try
        {
            await audio.ConvertToWavAsync(inputPath, wavPath, ct);

            // Đo trên file WAV SAU khi chuyển, không đo file gốc. Trình duyệt ghi WebM theo kiểu
            // phát trực tiếp nên phần đầu file thường không ghi thời lượng — ffprobe đọc file gốc
            // sẽ ra "N/A". File WAV thì luôn ghi đủ thời lượng ngay trong phần đầu.
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

    private async Task<IReadOnlyList<QcIssueDto>> RunQualityChecksAsync(AudioProbeResult probe, CancellationToken ct)
    {
        var min = await config.GetDecimalAsync(ConfigKeys.RecordingMinDurationSec, 1m, ct);
        var max = await config.GetDecimalAsync(ConfigKeys.RecordingMaxDurationSec, 30m, ct);

        var issues = new List<QcIssueDto>();
        var actual = probe.DurationSec.ToString("0.##", CultureInfo.InvariantCulture);

        if (probe.DurationSec < min)
        {
            issues.Add(new QcIssueDto("too_short",
                $"Bản ghi dài {actual} giây, ngắn hơn mức tối thiểu {min.ToString(CultureInfo.InvariantCulture)} giây."));
        }

        if (probe.DurationSec > max)
        {
            issues.Add(new QcIssueDto("too_long",
                $"Bản ghi dài {actual} giây, dài hơn mức tối đa {max.ToString(CultureInfo.InvariantCulture)} giây."));
        }

        return issues;
    }

    private async Task<Recording> LoadAccessibleAsync(long recordingId, long? ownerOnlyUserId, CancellationToken ct)
    {
        var recording = await recordings.GetAsync(recordingId, ct);

        // Speaker hỏi bản ghi của người khác thì trả 404 như thể không tồn tại, không trả 403 —
        // trả 403 là vô tình xác nhận rằng id đó có thật.
        if (recording is null || (ownerOnlyUserId.HasValue && recording.SpeakerId != ownerOnlyUserId.Value))
        {
            throw new NotFoundException("recording_not_found", $"Không tìm thấy bản ghi #{recordingId}.");
        }

        return recording;
    }

    /// <summary>
    /// Chỉ lấy phần đuôi file để ffmpeg đoán định dạng dễ hơn. Tên file do client gửi
    /// KHÔNG BAO GIỜ được dùng làm đường dẫn — chặn kiểu tấn công ../../ ghi đè file hệ thống.
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
