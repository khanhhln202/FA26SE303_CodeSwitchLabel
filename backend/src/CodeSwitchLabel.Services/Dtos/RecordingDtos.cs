using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Audio;

namespace CodeSwitchLabel.Services.Dtos;

public record RecordingDto(
    string RecordingId,
    string ScriptId,
    SentenceVariant SentenceVariant,
    long SpeakerId,
    long? TaskId,
    RecordingStatus Status,
    string AudioFormat,
    decimal DurationSec,
    DateTimeOffset RecordedAt);

/// <summary>Một lỗi của bước kiểm tra tự động; trả trong response và nằm trong qc_metrics đã lưu.</summary>
public record QcIssueDto(string Code, string Message);

/// <summary>
/// Kết quả nộp bản ghi. Trượt kiểm tra tự động vẫn là 201: bản ghi ĐÃ được tạo với trạng thái
/// qc_failed, chỉ là không vào hàng đợi của Reviewer.
/// </summary>
/// <param name="Take">Lần thu thứ mấy cho cặp câu và biến thể này. Lần đầu là 1.</param>
/// <param name="QcMetrics">Kết quả đầy đủ của bước kiểm tra tự động — cùng dữ liệu đã ghi vào recording.qc_metrics.</param>
public record UploadRecordingResult(
    RecordingDto Recording,
    bool QcPassed,
    IReadOnlyList<QcIssueDto> QcIssues,
    int Take,
    RecordingQcReport QcMetrics);

public record AudioUrlDto(string Url, DateTimeOffset ExpiresAt);

public record RecordingSearchRequest : PageRequest
{
    public RecordingStatus? Status { get; init; }
    public string? ScriptId { get; init; }

    /// <summary>Chỉ Admin, Reviewer và Task Manager lọc được theo người đọc.</summary>
    public long? SpeakerId { get; init; }
}

/// <summary>
/// Dữ liệu nộp bản ghi, tách khỏi IFormFile của ASP.NET để tầng Service không phụ thuộc web.
/// </summary>
public record UploadRecordingCommand(
    long SpeakerId,
    string ScriptId,
    SentenceVariant SentenceVariant,
    long? TaskId,
    Stream Audio,
    string FileName,
    long Length);
