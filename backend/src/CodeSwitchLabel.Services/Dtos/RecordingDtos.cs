using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

public record RecordingDto(
    long RecordingId,
    long ScriptId,
    long SpeakerId,
    long? TaskId,
    RecordingStatus Status,
    string AudioFormat,
    decimal DurationSec,
    DateTimeOffset RecordedAt);

/// <summary>Một lỗi của bước kiểm tra tự động. Chỉ trả trong response — ERD không có bảng lưu.</summary>
public record QcIssueDto(string Code, string Message);

/// <summary>
/// Kết quả nộp bản ghi. Trượt kiểm tra tự động vẫn là 201 Created: bản ghi ĐÃ được tạo
/// với trạng thái qc_failed, chỉ là không vào hàng đợi của Reviewer.
/// Frontend đọc qcPassed để báo người đọc thu lại.
/// </summary>
public record UploadRecordingResult(
    RecordingDto Recording,
    bool QcPassed,
    IReadOnlyList<QcIssueDto> QcIssues);

public record AudioUrlDto(string Url, DateTimeOffset ExpiresAt);

public record RecordingSearchRequest : PageRequest
{
    public RecordingStatus? Status { get; init; }
}

/// <summary>
/// Dữ liệu nộp bản ghi, tách khỏi IFormFile của ASP.NET để tầng Service
/// không phụ thuộc web — test được bằng một luồng dữ liệu bất kỳ.
/// </summary>
public record UploadRecordingCommand(
    long SpeakerId,
    long ScriptId,
    long? TaskId,
    Stream Audio,
    string FileName,
    long Length);
