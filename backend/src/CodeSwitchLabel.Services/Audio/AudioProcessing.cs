using System.ComponentModel.DataAnnotations;

namespace CodeSwitchLabel.Services.Audio;

public class AudioOptions
{
    public const string SectionName = "Audio";

    /// <summary>Để "ffmpeg" thì tìm trong PATH. Máy nào cài chỗ khác thì ghi đường dẫn đầy đủ.</summary>
    [Required]
    public string FfmpegPath { get; set; } = "ffmpeg";

    [Required]
    public string FfprobePath { get; set; } = "ffprobe";

    /// <summary>16 kHz mono là định dạng đầu vào chuẩn của hầu hết mô hình ASR hiện nay.</summary>
    [Range(8000, 48000)]
    public int TargetSampleRate { get; set; } = 16000;

    [Range(1, 2)]
    public int TargetChannels { get; set; } = 1;

    /// <summary>ffmpeg treo thì dừng sau chừng này giây, không để request chờ vô hạn.</summary>
    [Range(5, 600)]
    public int ProcessTimeoutSeconds { get; set; } = 60;

    [Range(1, 100)]
    public int MaxUploadMegabytes { get; set; } = 20;
}

public record AudioProbeResult(decimal DurationSec, int? SampleRate, int? Channels, string? CodecName);

public interface IAudioProcessor
{
    /// <summary>Chuyển mọi định dạng đầu vào sang WAV PCM 16-bit theo cấu hình.</summary>
    Task ConvertToWavAsync(string inputPath, string outputPath, CancellationToken ct = default);

    /// <summary>Đọc thời lượng, tần số lấy mẫu, số kênh của một file âm thanh.</summary>
    Task<AudioProbeResult> ProbeAsync(string filePath, CancellationToken ct = default);
}

/// <summary>
/// ffmpeg hoặc ffprobe không xử lý được file — thường vì thứ được gửi lên
/// không phải âm thanh. Đây là lỗi của dữ liệu đầu vào, không phải của máy chủ.
/// </summary>
public class AudioProcessingException(string message, Exception? inner = null) : Exception(message, inner);
