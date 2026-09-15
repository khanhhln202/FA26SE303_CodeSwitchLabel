using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Services.Audio;

/// <summary>
/// Gọi ffmpeg và ffprobe như tiến trình ngoài.
///
/// Vì sao không dùng thư viện .NET thuần: trình duyệt ghi âm ra WebM/Opus,
/// và thư viện .NET gần như không đọc được định dạng đó. ffmpeg thì đọc được mọi thứ.
/// </summary>
public sealed class FfmpegAudioProcessor(
    IOptions<AudioOptions> options,
    ILogger<FfmpegAudioProcessor> logger) : IAudioProcessor
{
    private readonly AudioOptions _options = options.Value;

    public async Task ConvertToWavAsync(string inputPath, string outputPath, CancellationToken ct = default)
    {
        await RunAsync(_options.FfmpegPath,
        [
            "-hide_banner", "-loglevel", "error",
            "-y",                                   // ghi đè file đích nếu đã có
            "-i", inputPath,
            "-vn",                                  // bỏ luồng hình nếu có
            "-ac", _options.TargetChannels.ToString(CultureInfo.InvariantCulture),
            "-ar", _options.TargetSampleRate.ToString(CultureInfo.InvariantCulture),
            "-c:a", "pcm_s16le",                    // WAV 16-bit không nén
            outputPath
        ], ct);

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
        {
            throw new AudioProcessingException("ffmpeg chạy xong nhưng không sinh ra file WAV.");
        }
    }

    public async Task<AudioProbeResult> ProbeAsync(string filePath, CancellationToken ct = default)
    {
        var json = await RunAsync(_options.FfprobePath,
        [
            "-v", "error",
            "-print_format", "json",
            "-show_format",
            "-show_streams",
            "-select_streams", "a:0",
            filePath
        ], ct);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("streams", out var streams) || streams.GetArrayLength() == 0)
            {
                throw new AudioProcessingException("File không có luồng âm thanh nào.");
            }

            var stream = streams[0];

            var duration = ReadDecimal(root, "format", "duration")
                           ?? ReadDecimal(stream, "duration")
                           ?? throw new AudioProcessingException("ffprobe không đọc được thời lượng.");

            return new AudioProbeResult(
                Math.Round(duration, 2),
                ReadInt(stream, "sample_rate"),
                ReadInt(stream, "channels"),
                stream.TryGetProperty("codec_name", out var codec) ? codec.GetString() : null);
        }
        catch (JsonException ex)
        {
            throw new AudioProcessingException("Không đọc được kết quả trả về của ffprobe.", ex);
        }
    }

    private async Task<string> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // ArgumentList tự bọc từng tham số — không ghép thành một chuỗi lệnh,
        // nên đường dẫn có dấu cách hay ký tự lạ cũng không phá được câu lệnh.
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new AudioProcessingException($"Không khởi động được {fileName}.");
            }
        }
        catch (Win32Exception ex)
        {
            // Lỗi cài đặt máy chủ chứ không phải lỗi người dùng — ném loại khác để trả 500,
            // kèm thông báo nói rõ phải sửa gì.
            throw new InvalidOperationException(
                $"Không tìm thấy '{fileName}'. Cài ffmpeg hoặc đặt đường dẫn trong mục cấu hình Audio.", ex);
        }

        // Đọc hai luồng SONG SONG. Đọc lần lượt sẽ treo cứng khi ffmpeg ghi đầy
        // bộ đệm stderr trong lúc mình còn đang chờ stdout.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.ProcessTimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);

            if (ct.IsCancellationRequested) throw;

            throw new AudioProcessingException(
                $"{Path.GetFileName(fileName)} chạy quá {_options.ProcessTimeoutSeconds} giây nên đã bị dừng.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            logger.LogInformation(
                "{Tool} thoát với mã {ExitCode}: {Error}",
                Path.GetFileName(fileName), process.ExitCode, stderr.Trim());

            throw new AudioProcessingException(
                $"{Path.GetFileName(fileName)} không xử lý được file (mã thoát {process.ExitCode}).");
        }

        return stdout;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Tiến trình vừa tự thoát giữa lúc định dừng nó — không cần làm gì.
        }
    }

    /// <summary>ffprobe trả số dưới dạng chuỗi, và có thể là "N/A" khi không đọc được.</summary>
    private static decimal? ReadDecimal(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var name in path)
        {
            if (!current.TryGetProperty(name, out current)) return null;
        }

        var raw = current.ValueKind == JsonValueKind.String ? current.GetString() : current.GetRawText();

        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;

        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }
}
