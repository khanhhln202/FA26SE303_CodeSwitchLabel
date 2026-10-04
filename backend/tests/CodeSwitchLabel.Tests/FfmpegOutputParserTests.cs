using CodeSwitchLabel.Services.Audio;
using Xunit;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Log của ffmpeg là hợp đồng ngầm với silencedetect/volumedetect —
/// test ghim lại định dạng thật để lần nâng ffmpeg sau biết ngay nếu chuỗi đổi.
/// </summary>
public sealed class FfmpegOutputParserTests
{
    [Fact]
    public void Parse_WhenLeadingAndTrailingSilence_MeasuresBothEdges()
    {
        const string stderr = """
            [Parsed_silencedetect_0 @ 0x55d1] silence_start: 0
            [Parsed_silencedetect_0 @ 0x55d1] silence_end: 1.44 | silence_duration: 1.44
            [Parsed_silencedetect_0 @ 0x55d1] silence_start: 3.9
            [Parsed_silencedetect_0 @ 0x55d1] silence_end: 5 | silence_duration: 1.1
            [Parsed_volumedetect_1 @ 0x55d1] mean_volume: -23.4 dB
            [Parsed_volumedetect_1 @ 0x55d1] max_volume: -1.1 dB
            """;

        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 5m);

        Assert.Equal(1.44m, metrics.LeadingSilenceSec);
        Assert.Equal(1.1m, metrics.TrailingSilenceSec);
        Assert.Equal(-23.4m, metrics.MeanVolumeDb);
        Assert.Equal(-1.1m, metrics.MaxVolumeDb);
        Assert.False(metrics.ClippingSuspected);
    }

    [Fact]
    public void Parse_WhenSilenceOnlyInMiddle_EdgesStayZero()
    {
        const string stderr = """
            [Parsed_silencedetect_0 @ 0x55d1] silence_start: 1.0
            [Parsed_silencedetect_0 @ 0x55d1] silence_end: 1.8 | silence_duration: 0.8
            """;

        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 4m);

        Assert.Equal(0m, metrics.LeadingSilenceSec);
        Assert.Equal(0m, metrics.TrailingSilenceSec);
        Assert.Null(metrics.MeanVolumeDb);
    }

    [Fact]
    public void Parse_WhenFileEndsMidSilence_ClosesAtDuration()
    {
        // Ghi xong bấm dừng khi đang im lặng: ffmpeg không in silence_end cho đoạn cuối.
        const string stderr = """
            [Parsed_silencedetect_0 @ 0x55d1] silence_start: 4.2
            """;

        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 5m);

        Assert.Equal(0m, metrics.LeadingSilenceSec);
        Assert.Equal(0.8m, metrics.TrailingSilenceSec);
    }

    [Fact]
    public void Parse_WhenPeakReachesZeroDb_MarksClipping()
    {
        const string stderr = """
            [Parsed_volumedetect_1 @ 0x55d1] mean_volume: -8.0 dB
            [Parsed_volumedetect_1 @ 0x55d1] max_volume: 0.0 dB
            """;

        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 3m);

        Assert.True(metrics.ClippingSuspected);
    }

    [Fact]
    public void Parse_WhenSilenceOverrunsFile_ClampsAndRounds()
    {
        const string stderr = """
            [Parsed_silencedetect_0 @ 0x55d1] silence_start: 0
            [Parsed_silencedetect_0 @ 0x55d1] silence_end: 5.004 | silence_duration: 5.004
            """;

        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 5m);

        Assert.Equal(5m, metrics.LeadingSilenceSec);
        Assert.Equal(5m, metrics.TrailingSilenceSec);
    }
}
