namespace CodeSwitchLabel.Repositories.Enums;

// Toàn bộ enum dưới đây chép đúng từ ERD nhóm đã chốt (bản 15/09/2026).
// Giá trị số ghim cố định vì nó nằm trong database — đổi số là hỏng dữ liệu cũ.
// Muốn thêm giá trị thì thêm ở cuối, không chèn vào giữa.

public enum RoleName
{
    Speaker = 0,
    Reviewer = 1,
    TaskManager = 2,
    Admin = 3
}

public enum UserStatus
{
    Active = 0,
    Inactive = 1
}

public enum SpeakerRegion
{
    North = 0,
    Central = 1,
    South = 2
}

public enum EnglishLevel
{
    Beginner = 0,
    Intermediate = 1,
    Advanced = 2
}

public enum Occupation
{
    Student = 0,
    Employed = 1,
    Other = 2
}

/// <summary>
/// Vòng đời script. completed = đã thu đủ số lượng cần;
/// deactivated = Admin rút khỏi lưu thông nhưng không xoá.
/// </summary>
public enum ScriptStatus
{
    PendingValidation = 0,
    Validated = 1,
    Completed = 2,
    Rejected = 3,
    Deactivated = 4
}

/// <summary>Chủ đề của script — dùng để cân bằng phân bố chủ đề trong corpus.</summary>
public enum ScriptDomain
{
    ItTechnology = 0,
    Education = 1,
    DailyLife = 2
}

/// <summary>Kết quả Speaker duyệt nội dung script trước khi thu.</summary>
public enum ScriptReviewAction
{
    Accepted = 0,
    Edited = 1,
    Rejected = 2
}

public enum ImportBatchStatus
{
    Processing = 0,
    Completed = 1,
    Failed = 2
}

public enum TaskType
{
    Recording = 0,
    Review = 1
}

public enum WorkTaskStatus
{
    Draft = 0,
    Open = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum AssignmentStatus
{
    Active = 0,
    Completed = 1,
    Reassigned = 2,
    Cancelled = 3
}

public enum TaskScriptStatus
{
    Pending = 0,
    Completed = 1,
    Rejected = 2
}

public enum TaskRecordingStatus
{
    Queued = 0,
    Reviewed = 1,
    Skipped = 2
}

/// <summary>
/// qc_failed = trượt kiểm tra tự động, chưa từng vào hàng đợi của Reviewer.
/// Tách khỏi rejected để thống kê phân biệt được "máy loại" với "người loại".
/// </summary>
public enum RecordingStatus
{
    QcFailed = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3
}

public enum ReviewDecision
{
    Approved = 0,
    Rejected = 1
}

/// <summary>Bốn nhóm lỗi theo ERD — ba nhóm đầu lấy từ mô tả đề tài, thêm nhóm other.</summary>
public enum RejectionCategory
{
    Content = 0,
    AudioQuality = 1,
    Pronunciation = 2,
    Other = 3
}

public enum DatasetStatus
{
    Draft = 0,
    Released = 1,
    Archived = 2
}

public enum DatasetFileFormat
{
    Json = 0,
    Csv = 1
}

public enum NotificationType
{
    ErrorWarning = 0,
    TaskRequest = 1,
    System = 2
}

public enum AuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Import = 3,
    Export = 4,
    Assign = 5,
    Release = 6,
    Login = 7
}

public enum ConfigValueType
{
    Int = 0,
    Bool = 1,
    String = 2
}
