using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

public record ScriptListItemDto(
    long ScriptId,
    string Content,
    ScriptStatus Status,
    ScriptDomain Domain,
    int WordCount,
    int EnWordCount,
    DateTimeOffset CreatedAt);

public record ScriptDetailDto(
    long ScriptId,
    string Content,
    ScriptStatus Status,
    ScriptDomain Domain,
    int WordCount,
    int EnWordCount,
    long CreatedById,
    long? ImportBatchId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ScriptReviewDto> Reviews);

public record ScriptReviewDto(
    long ScriptReviewId,
    long UserId,
    ScriptReviewAction Action,
    string? ErrorReasonCode,
    string? EditedContent,
    string? Comment,
    DateTimeOffset ReviewedAt);

/// <summary>Script phát cho Speaker đọc, kèm ngưỡng thời lượng để giao diện chặn sớm.</summary>
public record NextScriptDto(
    long ScriptId,
    string Content,
    ScriptDomain Domain,
    int WordCount,
    int EnWordCount,
    RecordingGuidanceDto Guidance);

public record RecordingGuidanceDto(decimal MinDurationSec, decimal MaxDurationSec);

public record CreateScriptRequest
{
    [Required(ErrorMessage = "Nội dung script không được để trống.")]
    [StringLength(1000, MinimumLength = 3)]
    public string Content { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phải chọn chủ đề cho script.")]
    public ScriptDomain? Domain { get; init; }

    /// <summary>
    /// Để trống thì hệ thống tự ước lượng số từ tiếng Anh.
    /// Ước lượng chỉ là phỏng đoán nên người nhập được phép ghi đè.
    /// </summary>
    [Range(0, 1000)]
    public int? EnWordCount { get; init; }
}

public record ReviewScriptRequest
{
    [Required(ErrorMessage = "Phải chọn kết quả duyệt.")]
    public ScriptReviewAction? Action { get; init; }

    /// <summary>Bắt buộc khi Action = Edited.</summary>
    [StringLength(1000, MinimumLength = 3)]
    public string? EditedContent { get; init; }

    /// <summary>Bắt buộc khi Action = Rejected. Lấy từ danh mục script-error-reasons.</summary>
    [StringLength(50)]
    public string? ErrorReasonCode { get; init; }

    [StringLength(1000)]
    public string? Comment { get; init; }
}

public record SkipScriptRequest
{
    /// <summary>Không bắt buộc — bỏ qua khác với từ chối, người đọc không cần giải trình.</summary>
    [StringLength(500)]
    public string? Note { get; init; }
}

public record ScriptSearchRequest : PageRequest
{
    public ScriptStatus? Status { get; init; }
    public ScriptDomain? Domain { get; init; }
    public string? Keyword { get; init; }
}

public record ReasonDto(short ReasonId, string ReasonCode, string? Category, string? Description, bool IsActive);
