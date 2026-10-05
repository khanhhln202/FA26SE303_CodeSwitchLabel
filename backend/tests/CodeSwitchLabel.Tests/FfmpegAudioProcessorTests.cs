using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using CodeSwitchLabel.Services.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Test tích hợp với ffmpeg THẬT, không giả lập.
///
/// Giả lập ffmpeg chềEkiểm được rằng code truyền đúng tham sềE Ekhông kiểm được điều
/// quan trọng thật sự: ffmpeg có xử lý file của trình duyệt đúng như ta giả định không.
///
/// Máy chưa cài ffmpeg thì nhóm này tự bềEqua (Skip), không làm đềEsuite:
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

    private readonly bool _ffmpegAvailable;

    public FfmpegAudioProcessorTests()
    {
        _ffmpegAvailable = IsFfmpegAvailable();
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
            // Thư mục tạm  EhềEđiều hành sẽ dọn sau.
        }
        catch (UnauthorizedAccessException)
        {
            // File còn bềEffmpeg giữ trên Windows  EbềEqua, lần chạy sau dùng GUID mới.
        }
    }

    private bool SkipIfNoFfmpeg()
    {
        // xunit.v3 ềErepo này không có API Skip động, nên test tự bềEqua êm khi thiếu ffmpeg:
        // return true nghĩa là "không chạy tiếp", suite vẫn xanh, log lý do trong comment.
        return !_ffmpegAvailable;
    }

    /// <summary>
    /// Lý do tồn tại của bước "chuyển sang WAV rồi mới đo".
    /// Trình duyệt ghi WebM theo kiểu phát trực tiếp, không tua lại đầu file đềEghi thời lượng,
    /// nên đo thẳng file gốc là không được. Nếu test này có ngày không còn đúng nữa,
    /// thì mới được bàn tới chuyện bềEbước chuyển đổi.
    /// </summary>
    [Fact]
    public async Task FileGhiNhuTrinhDuyet_KhongDoDuocThoiLuongTuFileGoc()
    {
        // Arrange
        if (SkipIfNoFfmpeg()) return;
        var webm = await GenerateBrowserLikeWebmAsync(seconds: 3);

        // Act
        var act = () => _processor.ProbeAsync(webm);

        // Assert
        await Assert.ThrowsAsync<AudioProcessingException>(act);
    }

    [Fact]
    public async Task ChuyenSangWavRoiMoiDo_RaDungThoiLuongVaDinhDang()
    {
        // Arrange
        if (SkipIfNoFfmpeg()) return;
        var webm = await GenerateBrowserLikeWebmAsync(seconds: 3);
        var wav = Path.Combine(_workDir, "out.wav");

        // Act
        await _processor.ConvertToWavAsync(webm, wav);
        var probe = await _processor.ProbeAsync(wav);

        // Assert  Effmpeg mã hoá không chính xác tới mili-giây nên dùng khoảng thay vì bằng tuyệt đối.
        Assert.InRange(probe.DurationSec, 2.9m, 3.1m);
        Assert.Equal(16000, probe.SampleRate);
        Assert.Equal(1, probe.Channels);
        Assert.Equal("pcm_s16le", probe.CodecName);
    }

    [Fact]
    public async Task FileKhongPhaiAmThanh_NemAudioProcessingException()
    {
        // Arrange
        if (SkipIfNoFfmpeg()) return;
        var fake = Path.Combine(_workDir, "fake.webm");
        await File.WriteAllTextAsync(fake, "đây chềElà chữ, không phải âm thanh");

        // Act  Ephải là AudioProcessingException chứ không phải lỗi chung chung,
        // vì tầng Service dựa vào đúng loại này đềEtrả 422 thay vì 500.
        var act = () => _processor.ConvertToWavAsync(fake, Path.Combine(_workDir, "fake.wav"));

        // Assert
        await Assert.ThrowsAsync<AudioProcessingException>(act);
    }

    [Fact]
    public async Task FileKhongTonTai_NemAudioProcessingException()
    {
        // Arrange
        if (SkipIfNoFfmpeg()) return;
        var missing = Path.Combine(_workDir, "khong-ton-tai.webm");

        // Act
        var act = () => _processor.ProbeAsync(missing);

        // Assert
        await Assert.ThrowsAsync<AudioProcessingException>(act);
    }

    [Fact]
    public async Task HuyGiuaChung_NemOperationCanceled()
    {
        // Arrange
        if (SkipIfNoFfmpeg()) return;
        var webm = await GenerateBrowserLikeWebmAsync(seconds: 3);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _processor.ConvertToWavAsync(webm, Path.Combine(_workDir, "cancelled.wav"), cts.Token);

        // Assert  Eprocessor phải tôn trọng token huỷ thay vì chạy hết file.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }

    /// <summary>
    /// Chính thư mục dự án có dấu cách ("FPT-learning Materials"), nên đây không phải ca hiếm.
    /// Kiểm rằng việc truyền từng tham sềEriêng qua ArgumentList không đềEđường dẫn phá câu lệnh.
    /// </summary>
    [Fact]
    public async Task DuongDanCoDauCachVaTiengViet_VanXuLyDuoc()
    {
        // Arrange
        if (SkipIfNoFfmpeg()) return;
        var dir = Path.Combine(_workDir, "thư mục có dấu cách");
        Directory.CreateDirectory(dir);

        var webm = await GenerateBrowserLikeWebmAsync(seconds: 1, directory: dir);
        var wav = Path.Combine(dir, "kết quả.wav");

        // Act
        await _processor.ConvertToWavAsync(webm, wav);

        // Assert
        Assert.True(File.Exists(wav));
        Assert.InRange((await _processor.ProbeAsync(wav)).DurationSec, 0.9m, 1.1m);
    }

    [Fact]
    public async Task DoTinHieu_DoDuocKhoangLangDauCuoiVaAmLuong()
    {
        // Arrange
        if (SkipIfNoFfmpeg()) return;
        var wav = await GenerateSilenceToneSilenceWavAsync(leadSec: 1.5, toneSec: 2, trailSec: 1.2);

        // Act
        var probe = await _processor.ProbeAsync(wav);
        var metrics = await _processor.AnalyzeSignalAsync(wav, probe.DurationSec, -35m, 0.5m);

        // Assert
        Assert.InRange(metrics.LeadingSilenceSec, 1.35m, 1.65m);
        Assert.InRange(metrics.TrailingSilenceSec, 1.05m, 1.35m);
        Assert.NotNull(metrics.MeanVolumeDb);
        Assert.NotNull(metrics.MaxVolumeDb);
        Assert.False(metrics.ClippingSuspected);
    }

    [Fact]
    public async Task DoTinHieu_FileChiCoTieng_KhongCoKhoangLangBien()
    {
        // Arrange
        if (SkipIfNoFfmpeg()) return;
        var webm = await GenerateBrowserLikeWebmAsync(seconds: 2);
        var wav = Path.Combine(_workDir, "tone.wav");

        await _processor.ConvertToWavAsync(webm, wav);

        // Act
        var probe = await _processor.ProbeAsync(wav);
        var metrics = await _processor.AnalyzeSignalAsync(wav, probe.DurationSec, -35m, 0.5m);

        // Assert  Etiếng liên tục: hai mép không có khoảng lặng đáng kềE(chềEvài ms lúc bềElọc bắt đầu).
        Assert.InRange(metrics.LeadingSilenceSec, 0m, 0.2m);
        Assert.InRange(metrics.TrailingSilenceSec, 0m, 0.2m);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Sinh file WebM/Opus bằng cách ghi ra pipe  Effmpeg không tua lại được đầu file,
    /// y hệt cách MediaRecorder của trình duyệt ghi âm.
    /// </summary>
    private async Task<string> GenerateBrowserLikeWebmAsync(int seconds, string? directory = null)
    {
        var path = Path.Combine(directory ?? _workDir, $"browser-{Guid.NewGuid():N}.webm");

        var exit = await RunFfmpegAsync(
            ["-hide_banner", "-loglevel", "error",
             "-f", "lavfi", "-i", $"sine=frequency=440:duration={seconds}",
             "-c:a", "libopus", "-b:a", "32k",
             "-f", "webm", "pipe:1"],
            stdoutToFile: path);

        Assert.True(exit == 0, "ffmpeg không sinh được file thử.");
        return path;
    }

    /// <summary>
    /// Sinh WAV đúng cấu trúc một lượt thu thật: im lặng đầu (bấm nút rồi mới nói),
    /// tiếng ềEgiữa, im lặng cuối (nói xong chưa bấm dừng).
    /// </summary>
    private async Task<string> GenerateSilenceToneSilenceWavAsync(double leadSec, double toneSec, double trailSec)
    {
        var path = Path.Combine(_workDir, $"silence-{Guid.NewGuid():N}.wav");
        var inv = CultureInfo.InvariantCulture;

        var exit = await RunFfmpegAsync(
            ["-hide_banner", "-loglevel", "error", "-y",
             "-f", "lavfi", "-i", $"anullsrc=r=16000:cl=mono:d={leadSec.ToString(inv)}",
             "-f", "lavfi", "-i", $"sine=frequency=440:r=16000:d={toneSec.ToString(inv)}",
             "-f", "lavfi", "-i", $"anullsrc=r=16000:cl=mono:d={trailSec.ToString(inv)}",
             "-filter_complex", "[0:a][1:a][2:a]concat=n=3:v=0:a=1[out]",
             "-map", "[out]",
             path]);

        Assert.True(exit == 0, "ffmpeg không sinh được file thử.");
        return path;
    }

    /// <summary>Chạy ffmpeg với từng tham sềEriêng qua ArgumentList  Eđường dẫn có dấu cách vẫn an toàn.</summary>
    /// <param name="stdoutToFile">Nếu có, chuyển stdout (pipe:1) ra file thay vì đọc vào bềEnhềE</param>
    private static async Task<int> RunFfmpegAsync(string[] arguments, string? stdoutToFile = null)
    {
        var startInfo = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardOutput = stdoutToFile is not null,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (stdoutToFile is not null)
        {
            await using var file = File.Create(stdoutToFile);
            await process.StandardOutput.BaseStream.CopyToAsync(file);
        }

        await process.WaitForExitAsync();

        var stderr = await stderrTask;
        Assert.True(process.ExitCode == 0, $"ffmpeg thoát mã {process.ExitCode}: {stderr}");
        return process.ExitCode;
    }

    private static bool IsFfmpegAvailable()
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
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
