using System.ComponentModel;
using System.Diagnostics;
using CodeSwitchLabel.Services.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Test tích hợp với ffmpeg THẬT, không giả lập.
///
/// Giả lập ffmpeg chỉ kiểm được rằng code truyền đúng tham số — không kiểm được điều
/// quan trọng thật sự: ffmpeg có xử lý file của trình duyệt đúng như ta giả định không.
///
/// Máy chưa cài ffmpeg thì bỏ qua nhóm này:
///   dotnet test --filter "Category!=RequiresFfmpeg"
/// </summary>
[Trait("Category", "RequiresFfmpeg")]
public sealed class FfmpegAudioProcessorTests : IDisposable
{
    private readonly string _workDir =
        Path.Combine(Path.GetTempPath(), "csl-audio-tests", Guid.NewGuid().ToString("N"));

    private readonly FfmpegAudioProcessor _processor = new(
        Options.Create(new AudioOptions()),
        NullLogger<FfmpegAudioProcessor>.Instance);

    public FfmpegAudioProcessorTests()
    {
        EnsureFfmpegAvailable();
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_workDir, recursive: true);
        }
        catch (IOException)
        {
            // Thư mục tạm — hệ điều hành sẽ dọn sau.
        }
    }

    /// <summary>
    /// Lý do tồn tại của bước "chuyển sang WAV rồi mới đo".
    /// Trình duyệt ghi WebM theo kiểu phát trực tiếp, không tua lại đầu file để ghi thời lượng,
    /// nên đo thẳng file gốc là không được. Nếu test này có ngày không còn đúng nữa,
    /// thì mới được bàn tới chuyện bỏ bước chuyển đổi.
    /// </summary>
    [Fact]
    public async Task FileGhiNhuTrinhDuyet_KhongDoDuocThoiLuongTuFileGoc()
    {
        var webm = await GenerateBrowserLikeWebmAsync(seconds: 3);

        await Assert.ThrowsAsync<AudioProcessingException>(() => _processor.ProbeAsync(webm));
    }

    [Fact]
    public async Task ChuyenSangWavRoiMoiDo_RaDungThoiLuongVaDinhDang()
    {
        var webm = await GenerateBrowserLikeWebmAsync(seconds: 3);
        var wav = Path.Combine(_workDir, "out.wav");

        await _processor.ConvertToWavAsync(webm, wav);
        var probe = await _processor.ProbeAsync(wav);

        Assert.Equal(3.00m, probe.DurationSec);
        Assert.Equal(16000, probe.SampleRate);
        Assert.Equal(1, probe.Channels);
        Assert.Equal("pcm_s16le", probe.CodecName);
    }

    [Fact]
    public async Task FileKhongPhaiAmThanh_NemAudioProcessingException()
    {
        var fake = Path.Combine(_workDir, "fake.webm");
        await File.WriteAllTextAsync(fake, "đây chỉ là chữ, không phải âm thanh");

        // Phải là AudioProcessingException chứ không phải lỗi chung chung,
        // vì tầng Service dựa vào đúng loại này để trả 422 thay vì 500.
        await Assert.ThrowsAsync<AudioProcessingException>(
            () => _processor.ConvertToWavAsync(fake, Path.Combine(_workDir, "fake.wav")));
    }

    /// <summary>
    /// Chính thư mục dự án có dấu cách ("FPT-learning Materials"), nên đây không phải ca hiếm.
    /// Kiểm rằng việc truyền từng tham số riêng qua ArgumentList không để đường dẫn phá câu lệnh.
    /// </summary>
    [Fact]
    public async Task DuongDanCoDauCachVaTiengViet_VanXuLyDuoc()
    {
        var dir = Path.Combine(_workDir, "thư mục có dấu cách");
        Directory.CreateDirectory(dir);

        var webm = await GenerateBrowserLikeWebmAsync(seconds: 1, directory: dir);
        var wav = Path.Combine(dir, "kết quả.wav");

        await _processor.ConvertToWavAsync(webm, wav);

        Assert.True(File.Exists(wav));
        Assert.Equal(1.00m, (await _processor.ProbeAsync(wav)).DurationSec);
    }

    /// <summary>
    /// Sinh file WebM/Opus bằng cách ghi ra pipe — ffmpeg không tua lại được đầu file,
    /// y hệt cách MediaRecorder của trình duyệt ghi âm.
    /// </summary>
    private async Task<string> GenerateBrowserLikeWebmAsync(int seconds, string? directory = null)
    {
        var path = Path.Combine(directory ?? _workDir, $"browser-{Guid.NewGuid():N}.webm");

        var startInfo = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        string[] arguments =
        [
            "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", $"sine=frequency=440:duration={seconds}",
            "-c:a", "libopus", "-b:a", "32k",
            "-f", "webm", "pipe:1"
        ];

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        var stderrTask = process.StandardError.ReadToEndAsync();

        await using (var file = File.Create(path))
        {
            await process.StandardOutput.BaseStream.CopyToAsync(file);
        }

        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, $"ffmpeg không sinh được file thử: {await stderrTask}");
        return path;
    }

    private static void EnsureFfmpegAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;

            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException(
                "Không tìm thấy ffmpeg trong PATH. Cài bằng 'winget install Gyan.FFmpeg' rồi mở terminal mới, " +
                "hoặc bỏ qua nhóm test này: dotnet test --filter \"Category!=RequiresFfmpeg\"");
        }
    }
}
