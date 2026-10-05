using CodeSwitchLabel.Services.Audio;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Log của ffmpeg là hợp đồng ngầm với silencedetect/volumedetect —
/// test ghim lại định dạng thật để lần nâng ffmpeg sau biết ngay nếu chuỗi đổi.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FfmpegOutputParserTests
{
    [Fact]
    public void Parse_WhenLeadingAndTrailingSilence_MeasuresBothEdges()
    {
        // Arrange
        const string stderr = """
            [Parsed_silencedetect_0 @ 0x55d1] silence_start: 0
            [Parsed_silencedetect_0 @ 0x55d1] silence_end: 1.44 | silence_duration: 1.44
            [Parsed_silencedetect_0 @ 0x55d1] silence_start: 3.9
            [Parsed_silencedetect_0 @ 0x55d1] silence_end: 5 | silence_duration: 1.1
            [Parsed_volumedetect_1 @ 0x55d1] mean_volume: -23.4 dB
            [Parsed_volumedetect_1 @ 0x55d1] max_volume: -1.1 dB
            """;

        // Act
        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 5m);

        // Assert
        Assert.Equal(1.44m, metrics.LeadingSilenceSec);
        Assert.Equal(1.1m, metrics.TrailingSilenceSec);
        Assert.Equal(-23.4m, metrics.MeanVolumeDb);
        Assert.Equal(-1.1m, metrics.MaxVolumeDb);
        Assert.False(metrics.ClippingSuspected);
    }

    [Fact]
    public void Parse_WhenSilenceOnlyInMiddle_EdgesStayZero()
    {
        // Arrange
        const string stderr = """
            [Parsed_silencedetect_0 @ 0x55d1] silence_start: 1.0
            [Parsed_silencedetect_0 @ 0x55d1] silence_end: 1.8 | silence_duration: 0.8
            """;

        // Act
        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 4m);

        // Assert
        Assert.Equal(0m, metrics.LeadingSilenceSec);
        Assert.Equal(0m, metrics.TrailingSilenceSec);
        Assert.Null(metrics.MeanVolumeDb);
    }

    [Fact]
    public void Parse_WhenFileEndsMidSilence_ClosesAtDuration()
    {
        // Arrange — ghi xong bấm dừng khi đang im lặng: ffmpeg không in silence_end cho đoạn cuối.
        const string stderr = """
            [Parsed_silencedetect_0 @ 0x55d1] silence_start: 4.2
            """;

        // Act
        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 5m);

        // Assert
        Assert.Equal(0m, metrics.LeadingSilenceSec);
        Assert.Equal(0.8m, metrics.TrailingSilenceSec);
    }

    [Fact]
    public void Parse_WhenPeakReachesZeroDb_MarksClipping()
    {
        // Arrange
        const string stderr = """
            [Parsed_volumedetect_1 @ 0x55d1] mean_volume: -8.0 dB
            [Parsed_volumedetect_1 @ 0x55d1] max_volume: 0.0 dB
            """;

        // Act
        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 3m);

        // Assert
        Assert.True(metrics.ClippingSuspected);
    }

    [Fact]
    public void Parse_WhenSilenceOverrunsFile_ClampsAndRounds()
    {
        // Arrange
        const string stderr = """
            [Parsed_silencedetect_0 @ 0x55d1] silence_start: 0
            [Parsed_silencedetect_0 @ 0x55d1] silence_end: 5.004 | silence_duration: 5.004
            """;

        // Act
        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 5m);

        // Assert
        Assert.Equal(5m, metrics.LeadingSilenceSec);
        Assert.Equal(5m, metrics.TrailingSilenceSec);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("chỉ là chữ, không phải log ffmpeg")]
    [InlineData("[Parsed_silencedetect_0] silence_start: xyz")]
    public void Parse_WhenLogRongHoacRac_MepVeKhongVaAmLuongNull(string stderr)
    {
        // Arrange + Act
        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 5m);

        // Assert
        Assert.Equal(0m, metrics.LeadingSilenceSec);
        Assert.Equal(0m, metrics.TrailingSilenceSec);
        Assert.Null(metrics.MeanVolumeDb);
        Assert.Null(metrics.MaxVolumeDb);
        Assert.False(metrics.ClippingSuspected);
    }

    [Theory]
    [InlineData("[Parsed_volumedetect_1 @ 0x55d1] mean_volume: n/a | max_volume: n/a")]
    [InlineData("[Parsed_volumedetect_1 @ 0x55d1] mean_volume: -inf dB\n[Parsed_volumedetect_1 @ 0x55d1] max_volume: -inf dB")]
    public void Parse_WhenFfmpegKhongDoDuocAmLuong_AmLuongNull(string stderr)
    {
        // Arrange + Act — file câm hoàn toàn: ffmpeg in n/a hoặc -inf thay vì số.
        var metrics = FfmpegOutputParser.ParseSignalMetrics(stderr, durationSec: 3m);

        // Assert
        Assert.Null(metrics.MeanVolumeDb);
        Assert.Null(metrics.MaxVolumeDb);
        Assert.False(metrics.ClippingSuspected);
    }
}
