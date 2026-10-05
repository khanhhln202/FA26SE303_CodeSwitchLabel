using System.Text.Json;
using CodeSwitchLabel.Services.Audio;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Luật QC là hàm thuần nên kiểm được từng nhánh ngưỡng mà không cần database hay ffmpeg.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RecordingQcEvaluatorTests
{
    private const decimal Min = 1m;
    private const decimal Max = 30m;
    private const decimal MaxLead = 1m;
    private const decimal MaxTrail = 1m;
    private const decimal QuietThresholdDb = -50m;
    private const decimal PeakThresholdDb = -1m;

    private static readonly AudioSignalMetrics Clean = new(0.1m, 0.2m, -23m, -1.5m, false);

    [Fact]
    public void Evaluate_WhenEverythingWithinThresholds_Passes()
    {
        // Arrange + Act
        var issues = RecordingQcEvaluator.Evaluate(5m, Clean, Min, Max, MaxLead, MaxTrail);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_WhenTooShort_ReportsTooShort()
    {
        // Arrange + Act
        var issues = RecordingQcEvaluator.Evaluate(0.5m, Clean, Min, Max, MaxLead, MaxTrail);

        // Assert
        var issue = Assert.Single(issues);
        Assert.Equal("too_short", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenTooLong_ReportsTooLong()
    {
        // Arrange + Act
        var issues = RecordingQcEvaluator.Evaluate(35m, Clean, Min, Max, MaxLead, MaxTrail);

        // Assert
        var issue = Assert.Single(issues);
        Assert.Equal("too_long", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenLeadingSilenceExceeds_ReportsLeadingSilence()
    {
        // Arrange
        var signal = Clean with { LeadingSilenceSec = 1.4m };

        // Act
        var issues = RecordingQcEvaluator.Evaluate(5m, signal, Min, Max, MaxLead, MaxTrail);

        // Assert
        var issue = Assert.Single(issues);
        Assert.Equal("excess_leading_silence", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenTrailingSilenceExceeds_ReportsTrailingSilence()
    {
        // Arrange
        var signal = Clean with { TrailingSilenceSec = 2.2m };

        // Act
        var issues = RecordingQcEvaluator.Evaluate(5m, signal, Min, Max, MaxLead, MaxTrail);

        // Assert
        var issue = Assert.Single(issues);
        Assert.Equal("excess_trailing_silence", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenValuesExactlyAtThresholds_Passes()
    {
        // Arrange — biên: phải VƯỢT ngưỡng mới là lỗi, bằng đúng ngưỡng thì đạt.
        var signal = new AudioSignalMetrics(MaxLead, MaxTrail, -23m, -1.5m, false);

        // Act
        var issues = RecordingQcEvaluator.Evaluate(Min, signal, Min, Max, MaxLead, MaxTrail);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_WhenSignalMissing_OnlyChecksDuration()
    {
        // Arrange + Act
        var issues = RecordingQcEvaluator.Evaluate(35m, signal: null, Min, Max, MaxLead, MaxTrail);

        // Assert
        var issue = Assert.Single(issues);
        Assert.Equal("too_long", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenSeveralProblems_ReportsAll()
    {
        // Arrange
        var signal = new AudioSignalMetrics(1.5m, 2.5m, -23m, -1.5m, false);

        // Act
        var issues = RecordingQcEvaluator.Evaluate(0.5m, signal, Min, Max, MaxLead, MaxTrail);

        // Assert
        Assert.Equal(
            ["too_short", "excess_leading_silence", "excess_trailing_silence"],
            issues.Select(i => i.Code));
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(-50.5)]
    public void Evaluate_WhenTooQuiet_ReportsTooQuiet(decimal meanDb)
    {
        // Arrange
        var signal = Clean with { MeanVolumeDb = meanDb };

        // Act
        var issues = RecordingQcEvaluator.Evaluate(5m, signal, Min, Max, MaxLead, MaxTrail, QuietThresholdDb, PeakThresholdDb);

        // Assert
        var issue = Assert.Single(issues);
        Assert.Equal("too_quiet", issue.Code);
    }

    [Theory]
    [InlineData(-0.2)]
    [InlineData(0)]
    public void Evaluate_WhenPeakExceedsMax_ReportsClipping(decimal peakDb)
    {
        // Arrange
        var signal = Clean with { MaxVolumeDb = peakDb };

        // Act
        var issues = RecordingQcEvaluator.Evaluate(5m, signal, Min, Max, MaxLead, MaxTrail, QuietThresholdDb, PeakThresholdDb);

        // Assert
        var issue = Assert.Single(issues);
        Assert.Equal("clipping_detected", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenClippingSuspectedButNoPeakThreshold_ReportsClipping()
    {
        // Arrange — không cấu hình maxPeakDb thì dựa vào cờ clipping của ffmpeg.
        var signal = Clean with { ClippingSuspected = true };

        // Act
        var issues = RecordingQcEvaluator.Evaluate(5m, signal, Min, Max, MaxLead, MaxTrail);

        // Assert
        Assert.Equal("clipping_detected", Assert.Single(issues).Code);
    }

    [Fact]
    public void Evaluate_WhenVolumeWithinThresholds_Passes()
    {
        // Arrange + Act
        var issues = RecordingQcEvaluator.Evaluate(5m, Clean, Min, Max, MaxLead, MaxTrail, QuietThresholdDb, PeakThresholdDb);

        // Assert
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    public void Evaluate_WhenDurationInvalid_ReportsTooShort(decimal duration)
    {
        // Arrange + Act — decimal không biểu diễn được NaN/Infinity nên chỉ còn ca âm và 0;
        // quan trọng là không ném mà báo too_short.
        var issues = RecordingQcEvaluator.Evaluate(duration, signal: null, Min, Max, MaxLead, MaxTrail);

        // Assert
        Assert.Contains(issues, i => i.Code == "too_short");
    }

    [Fact]
    public void Serialize_UsesSnakeCaseKeys_AndDropsNullFields()
    {
        // Arrange
        var report = new RecordingQcReport(true, 5.0m, 0.5m, null, -23m, null, false, []);

        // Act
        var json = RecordingQcJson.Serialize(report);

        // Assert — parse JSON thay vì Contains chuỗi để khỏi giòn khi đổi format số.
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("passed").GetBoolean());
        Assert.Equal(5.0m, root.GetProperty("duration_sec").GetDecimal());
        Assert.Equal(0.5m, root.GetProperty("leading_silence_sec").GetDecimal());
        Assert.Equal(-23m, root.GetProperty("mean_volume_db").GetDecimal());
        Assert.False(root.GetProperty("clipping_suspected").GetBoolean());

        // Trường null (không đo được) bị bỏ, không ghi null vào cột JSONB.
        Assert.False(root.TryGetProperty("trailing_silence_sec", out _));
        Assert.False(root.TryGetProperty("max_volume_db", out _));
    }
}
