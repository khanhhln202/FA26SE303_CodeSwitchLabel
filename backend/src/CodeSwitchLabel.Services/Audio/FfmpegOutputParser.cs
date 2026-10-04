using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeSwitchLabel.Services.Audio;

/// <summary>
/// Đọc kết quả của silencedetect + volumedetect từ log stderr của ffmpeg.
///
/// Tách khỏi FfmpegAudioProcessor để test được bằng chuỗi log thật mà không cần chạy ffmpeg:
/// định dạng log là hợp đồng ngầm với ffmpeg nên phải có test ghim lại.
/// </summary>
public static partial class FfmpegOutputParser
{
    /// <summary>Một khoảng lặng kết thúc cách mép file trong ngần này giây vẫn tính là ở mép.</summary>
    private const decimal EdgeToleranceSec = 0.05m;

    /// <summary>Đỉnh âm lượng chạm ngưỡng này (dBFS) thì nghi file bị vỡ tiếng do ghi quá to.</summary>
    private const decimal ClippingPeakDb = -0.1m;

    [GeneratedRegex(@"silence_start:\s*(?<value>-?\d+(?:\.\d+)?)")]
    private static partial Regex SilenceStartPattern();

    [GeneratedRegex(@"silence_end:\s*(?<value>-?\d+(?:\.\d+)?)")]
    private static partial Regex SilenceEndPattern();

    [GeneratedRegex(@"mean_volume:\s*(?<value>-?\d+(?:\.\d+)?)\s*dB")]
    private static partial Regex MeanVolumePattern();

    [GeneratedRegex(@"max_volume:\s*(?<value>-?\d+(?:\.\d+)?)\s*dB")]
    private static partial Regex MaxVolumePattern();

    /// <summary>
    /// Khoảng lặng bắt đầu trong 0.05 giây đầu file là khoảng lặng ĐẦU; khoảng lặng kết thúc trong
    /// 0.05 giây cuối file là khoảng lặng CUỐI. Khoảng lặng giữa câu không tính.
    /// File kết thúc giữa khoảng lặng thì ffmpeg không in silence_end — coi kéo tới hết file.
    /// </summary>
    public static AudioSignalMetrics ParseSignalMetrics(string stderr, decimal durationSec)
    {
        var segments = ParseSilenceSegments(stderr, durationSec);

        var leading = segments
            .Where(s => s.Start <= EdgeToleranceSec)
            .Select(s => s.End - s.Start)
            .FirstOrDefault();

        var trailing = segments
            .Where(s => s.End >= durationSec - EdgeToleranceSec)
            .Select(s => s.End - s.Start)
            .LastOrDefault();

        var meanVolume = FirstMatch(MeanVolumePattern(), stderr);
        var maxVolume = FirstMatch(MaxVolumePattern(), stderr);

        return new AudioSignalMetrics(
            Round(Clamp(leading, 0m, durationSec)),
            Round(Clamp(trailing, 0m, durationSec)),
            meanVolume,
            maxVolume,
            maxVolume >= ClippingPeakDb);
    }

    private static List<(decimal Start, decimal End)> ParseSilenceSegments(string stderr, decimal durationSec)
    {
        var segments = new List<(decimal Start, decimal End)>();
        decimal? openStart = null;

        foreach (var line in stderr.Split('\n'))
        {
            var start = TryMatch(SilenceStartPattern(), line);

            if (start.HasValue)
            {
                openStart = start;
                continue;
            }

            var end = TryMatch(SilenceEndPattern(), line);

            if (end.HasValue && openStart.HasValue && end.Value >= openStart.Value)
            {
                segments.Add((openStart.Value, end.Value));
                openStart = null;
            }
        }

        // File kết thúc khi đang im lặng: đóng khoảng lặng đang mở ở mốc hết file.
        if (openStart.HasValue && durationSec >= openStart.Value)
        {
            segments.Add((openStart.Value, durationSec));
        }

        return segments;
    }

    private static decimal? TryMatch(Regex pattern, string line)
    {
        var match = pattern.Match(line);

        return match.Success &&
               decimal.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                   CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static decimal? FirstMatch(Regex pattern, string stderr)
    {
        var match = pattern.Match(stderr);

        return match.Success &&
               decimal.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                   CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        Math.Min(Math.Max(value, min), max);

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
