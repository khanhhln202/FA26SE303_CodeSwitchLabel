namespace CodeSwitchLabel.Services.Dtos;

public record ReviewerTotalsDto(int ApprovedCount, int RejectedCount);

public record RejectReasonStatDto(string Label, double Percentage, string Count);

public record CategoryBreakdownDto(string Category, int Count);

public record TopRejectedSentenceDto(
    string ScriptId,
    string Text,
    string? ViText,
    IReadOnlyList<string> EnglishWords,
    string? Task,
    int Count,
    IReadOnlyList<CategoryBreakdownDto> Breakdown,
    string? Example);

public record TopRejectedSpeakerDto(
    long SpeakerId,
    string Name,
    int Count,
    string? TopReason,
    IReadOnlyList<CategoryBreakdownDto> Breakdown,
    string? Example);

/// <summary>Bản ghi tôi đã duyệt: quyết định của tôi + trạng thái chốt cuối.</summary>
public record ReviewerHistoryItemDto(
    string RecordingId,
    string ScriptId,
    long SpeakerId,
    string? SpeakerName,
    string? TaskTitle,
    string ScriptText,
    decimal DurationSec,
    string MyDecision,
    string RecordingStatus,
    DateTimeOffset ReviewedAt);

public record ReviewerHistoryQuery : PageRequest
{
    public long? TaskId { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
}

/// <summary>Câu tôi đã duyệt nội dung: hành động của tôi + trạng thái câu hiện tại.</summary>
public record ReviewerScriptHistoryItemDto(
    string ScriptId,
    string Kind,
    string Category,
    string CsTranscript,
    string ViEquivalent,
    string MyAction,
    string ScriptStatus,
    DateTimeOffset ReviewedAt);

public record ReviewerScriptHistoryQuery : PageRequest
{
    public string? Kind { get; init; }
    public string? Status { get; init; }
    public string? Category { get; init; }
    public string? Search { get; init; }
}
