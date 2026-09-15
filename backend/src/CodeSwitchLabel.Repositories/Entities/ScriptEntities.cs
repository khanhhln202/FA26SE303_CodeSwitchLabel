using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Đoạn văn bản để Speaker đọc. Trước đây ERD gọi là "sentence" —
/// đổi thành "script" theo yêu cầu của giảng viên.
/// </summary>
public class Script
{
    public long ScriptId { get; set; }

    public string Content { get; set; } = string.Empty;

    public ScriptStatus Status { get; set; } = ScriptStatus.PendingValidation;

    public int WordCount { get; set; }

    /// <summary>
    /// Số từ tiếng Anh trong câu — đây là con SỐ, không phải vị trí.
    /// Hệ thống không lưu từ nào nằm ở đâu, nên không tô màu được phần tiếng Anh
    /// và manifest dataset không xuất được nhãn theo từ. Đây là giới hạn đã biết
    /// của thiết kế hiện tại, nhóm quyết định hoãn xử lý.
    /// </summary>
    public int EnWordCount { get; set; }

    public ScriptDomain Domain { get; set; }

    public long CreatedById { get; set; }
    public AppUser CreatedBy { get; set; } = null!;

    /// <summary>NULL nghĩa là Admin nhập tay từng câu, không qua import file.</summary>
    public long? ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ScriptReview> Reviews { get; set; } = [];
    public ICollection<ScriptSkip> Skips { get; set; } = [];
    public ICollection<Recording> Recordings { get; set; } = [];
    public ICollection<TaskScript> TaskScripts { get; set; } = [];
}

/// <summary>Một lần Admin nạp file câu vào hệ thống. Có bảng này thì truy được câu nào từ file nào.</summary>
public class ImportBatch
{
    public long BatchId { get; set; }

    public long ImportedById { get; set; }
    public AppUser ImportedBy { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Processing;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Script> Scripts { get; set; } = [];
}

/// <summary>
/// Speaker duyệt nội dung script trước khi thu — use case "Review Text" của đề tài.
/// Khoá chính là cột thay thế chứ không phải (script, user), nên cùng một người
/// duyệt lại script đó lần nữa vẫn được, và lần trước vẫn còn trong lịch sử.
/// </summary>
public class ScriptReview
{
    public long ScriptReviewId { get; set; }

    public long ScriptId { get; set; }
    public Script Script { get; set; } = null!;

    public long UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Chỉ có giá trị khi Action = Rejected.</summary>
    public short? ErrorReasonId { get; set; }
    public ScriptErrorReason? ErrorReason { get; set; }

    public ScriptReviewAction Action { get; set; }

    /// <summary>Bắt buộc khi Action = Edited — ép bằng CHECK constraint.</summary>
    public string? EditedContent { get; set; }

    public string? Comment { get; set; }
    public DateTimeOffset ReviewedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Danh mục lý do Speaker từ chối một script. Chỉ ẩn được, không xoá cứng.</summary>
public class ScriptErrorReason
{
    public short ReasonId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ScriptReview> ScriptReviews { get; set; } = [];
}

/// <summary>
/// Speaker bỏ qua một script. Ghi lại để KHÔNG phát lại script đó cho chính người này.
/// Khác với ScriptReview ở chỗ: bỏ qua là "tôi không muốn đọc câu này",
/// còn từ chối là "câu này có vấn đề, ai đọc cũng không nên".
/// </summary>
public class ScriptSkip
{
    public long SkipId { get; set; }

    public long ScriptId { get; set; }
    public Script Script { get; set; } = null!;

    public long SpeakerId { get; set; }
    public AppUser Speaker { get; set; } = null!;

    /// <summary>NULL nghĩa là bỏ qua ngoài ngữ cảnh task nào.</summary>
    public long? TaskId { get; set; }
    public WorkTask? Task { get; set; }

    public DateTimeOffset SkippedAt { get; set; } = DateTimeOffset.UtcNow;
}
