using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Audio;

/// <summary>
/// Kết quả đầy đủ của một lượt kiểm tra tự động lúc nộp bản ghi. Đây chính là dữ liệu
/// ghi vào cột recording.qc_metrics — một nguồn cho cả response lẫn database.
/// </summary>
public record RecordingQcReport(
    bool Passed,
    decimal DurationSec,
    decimal? LeadingSilenceSec,
    decimal? TrailingSilenceSec,
    decimal? MeanVolumeDb,
    decimal? MaxVolumeDb,
    bool ClippingSuspected,
    IReadOnlyList<QcIssueDto> Issues);

/// <summary>
/// Luật kiểm tra tự động, viết thành hàm thuần để test được từng nhánh ngưỡng.
/// Thời lượng và khoảng lặng vượt ngưỡng đều là lỗi — bản ghi vào trạng thái qc_failed.
/// </summary>
public static class RecordingQcEvaluator
{
    public static IReadOnlyList<QcIssueDto> Evaluate(
        decimal durationSec,
        AudioSignalMetrics? signal,
        decimal minDurationSec,
        decimal maxDurationSec,
        decimal maxLeadingSilenceSec,
        decimal maxTrailingSilenceSec)
    {
        var issues = new List<QcIssueDto>();
        var actual = Format(durationSec);

        if (durationSec < minDurationSec)
        {
            issues.Add(new QcIssueDto("too_short",
                $"Bản ghi dài {actual} giây, ngắn hơn mức tối thiểu {Format(minDurationSec)} giây."));
        }

        if (durationSec > maxDurationSec)
        {
            issues.Add(new QcIssueDto("too_long",
                $"Bản ghi dài {actual} giây, dài hơn mức tối đa {Format(maxDurationSec)} giây."));
        }

        if (signal is not null)
        {
            if (signal.LeadingSilenceSec > maxLeadingSilenceSec)
            {
                issues.Add(new QcIssueDto("excess_leading_silence",
                    $"Khoảng lặng đầu bản ghi dài {Format(signal.LeadingSilenceSec)} giây, " +
                    $"vượt mức cho phép {Format(maxLeadingSilenceSec)} giây."));
            }

            if (signal.TrailingSilenceSec > maxTrailingSilenceSec)
            {
                issues.Add(new QcIssueDto("excess_trailing_silence",
                    $"Khoảng lặng cuối bản ghi dài {Format(signal.TrailingSilenceSec)} giây, " +
                    $"vượt mức cho phép {Format(maxTrailingSilenceSec)} giây."));
            }
        }

        return issues;
    }

    /// <summary>InvariantCulture bắt buộc: máy đặt tiếng Việt dùng dấu phẩy làm dấu thập phân.</summary>
    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>
/// Ghi kết quả QC vào cột qc_metrics. Khoá snake_case cho khớp quy ước database;
/// trường null bị bỏ để JSON gọn và câu truy vấn SQL dễ đọc.
/// </summary>
public static class RecordingQcJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(RecordingQcReport report) => JsonSerializer.Serialize(report, Options);
}
