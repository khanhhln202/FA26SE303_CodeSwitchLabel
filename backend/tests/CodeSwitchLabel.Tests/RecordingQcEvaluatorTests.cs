using CodeSwitchLabel.Services.Audio;
using Xunit;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Luật QC là hàm thuần nên kiểm được từng nhánh ngưỡng mà không cần database hay ffmpeg.
/// </summary>
public sealed class RecordingQcEvaluatorTests
{
    private const decimal Min = 1m;
    private const decimal Max = 30m;
    private const decimal MaxLead = 1m;
    private const decimal MaxTrail = 1m;

    private static readonly AudioSignalMetrics Clean = new(0.1m, 0.2m, -23m, -1.5m, false);

    [Fact]
    public void Evaluate_WhenEverythingWithinThresholds_Passes()
    {
        var issues = RecordingQcEvaluator.Evaluate(5m, Clean, Min, Max, MaxLead, MaxTrail);

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_WhenTooShort_ReportsTooShort()
    {
        var issues = RecordingQcEvaluator.Evaluate(0.5m, Clean, Min, Max, MaxLead, MaxTrail);

        var issue = Assert.Single(issues);
        Assert.Equal("too_short", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenTooLong_ReportsTooLong()
    {
        var issues = RecordingQcEvaluator.Evaluate(35m, Clean, Min, Max, MaxLead, MaxTrail);

        var issue = Assert.Single(issues);
        Assert.Equal("too_long", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenLeadingSilenceExceeds_ReportsLeadingSilence()
    {
        var signal = Clean with { LeadingSilenceSec = 1.4m };

        var issues = RecordingQcEvaluator.Evaluate(5m, signal, Min, Max, MaxLead, MaxTrail);

        var issue = Assert.Single(issues);
        Assert.Equal("excess_leading_silence", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenTrailingSilenceExceeds_ReportsTrailingSilence()
    {
        var signal = Clean with { TrailingSilenceSec = 2.2m };

        var issues = RecordingQcEvaluator.Evaluate(5m, signal, Min, Max, MaxLead, MaxTrail);

        var issue = Assert.Single(issues);
        Assert.Equal("excess_trailing_silence", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenValuesExactlyAtThresholds_Passes()
    {
        // Biên: phải VƯỢT ngưỡng mới là lỗi, bằng đúng ngưỡng thì đạt.
        var signal = new AudioSignalMetrics(MaxLead, MaxTrail, -23m, -1.5m, false);

        var issues = RecordingQcEvaluator.Evaluate(Min, signal, Min, Max, MaxLead, MaxTrail);

        Assert.Empty(issues);
    }

    [Fact]
    public void Evaluate_WhenSignalMissing_OnlyChecksDuration()
    {
        var issues = RecordingQcEvaluator.Evaluate(35m, signal: null, Min, Max, MaxLead, MaxTrail);

        var issue = Assert.Single(issues);
        Assert.Equal("too_long", issue.Code);
    }

    [Fact]
    public void Evaluate_WhenSeveralProblems_ReportsAll()
    {
        var signal = new AudioSignalMetrics(1.5m, 2.5m, -23m, -1.5m, false);

        var issues = RecordingQcEvaluator.Evaluate(0.5m, signal, Min, Max, MaxLead, MaxTrail);

        Assert.Equal(
            ["too_short", "excess_leading_silence", "excess_trailing_silence"],
            issues.Select(i => i.Code));
    }

    [Fact]
    public void Serialize_UsesSnakeCaseKeys_AndDropsNullFields()
    {
        var report = new RecordingQcReport(true, 5.0m, 0.5m, null, -23m, null, false, []);

        var json = RecordingQcJson.Serialize(report);

        Assert.Contains("\"passed\":true", json);
        Assert.Contains("\"duration_sec\":5.0", json);
        Assert.Contains("\"leading_silence_sec\":0.5", json);
        Assert.Contains("\"clipping_suspected\":false", json);

        // Trường null (không đo được) bị bỏ, không ghi null vào cột JSONB.
        Assert.DoesNotContain("trailing_silence_sec", json);
        Assert.DoesNotContain("max_volume_db", json);
    }
}
