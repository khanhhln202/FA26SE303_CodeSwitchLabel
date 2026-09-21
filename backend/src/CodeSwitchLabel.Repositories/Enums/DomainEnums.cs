namespace CodeSwitchLabel.Repositories.Enums;

// Mỗi enum dưới đây ứng với một kiểu ENUM thật trong docs/codeswitchlabel.sql.
// Tên kiểu và tên giá trị dịch sang snake_case: ItTechnology -> it_technology.
// Vì database lưu bằng TÊN chứ không phải số, thứ tự khai báo ở đây không quan trọng —
// nhưng đổi tên một giá trị là hỏng dữ liệu cũ.

public enum UserStatus { Active, Inactive }

public enum Occupation { Student, Employed, Other }

public enum ConfigValueType { Int, Bool, String }

public enum AuditAction { Create, Update, Delete, Import, Export, Assign, Release, Login }

/// <summary>
/// Vòng đời một cặp câu. Không còn trạng thái completed như bản ERD cũ:
/// lược đồ mới chỉ cần biết câu còn dùng được hay không.
/// </summary>
public enum ScriptStatus { PendingValidation, Validated, Rejected, Deactivated }

public enum ScriptDomain { ItTechnology, Education, DailyLife }

public enum ScriptReviewAction { Accepted, Edited, Rejected }

public enum TaskType { Recording, Review }

/// <summary>
/// Ứng với kiểu task_status. Đặt tên WorkTaskStatus để khỏi lẫn với
/// System.Threading.Tasks.TaskStatus, nên phải khai báo tên kiểu PostgreSQL một cách tường minh.
/// </summary>
public enum WorkTaskStatus { Draft, Open, InProgress, Completed, Cancelled }

public enum AssignmentStatus { Active, Completed, Reassigned, Cancelled }

public enum TaskScriptStatus { Pending, Completed, Rejected }

/// <summary>
/// Mỗi cặp câu cần hai bản ghi: một bản đọc câu chen tiếng Anh, một bản đọc câu thuần Việt.
/// Chữ cs/vi trong recording_id lấy từ đây.
/// </summary>
public enum SentenceVariant { CodeSwitching, PureVietnamese }

/// <summary>
/// qc_failed = trượt kiểm tra tự động, chưa từng vào hàng đợi duyệt.
/// Tách khỏi rejected để thống kê phân biệt được "máy loại" với "người loại".
/// </summary>
public enum RecordingStatus { QcFailed, PendingReview, Approved, Rejected }

public enum TaskRecordingStatus { Queued, Reviewed, Skipped }

public enum ReviewDecision { Approved, Rejected }

public enum RejectionCategory { Content, AudioQuality, Pronunciation, Other }

public enum DatasetStatus { Draft, Released, Archived }

public enum DatasetFileFormat { Json, Csv }

/// <summary>
/// Vai trò lưu trong bảng role dưới dạng chuỗi ('speaker', 'task_manager'...), không phải kiểu ENUM.
/// Dùng enum ở tầng code để [Authorize(Roles = "...")] và switch không bị gõ sai chuỗi.
/// </summary>
public enum RoleName { Speaker, Reviewer, TaskManager, Admin }

/// <summary>
/// Chữ số thứ ba trong script_id: quan hệ giữa phần tiếng Anh và nghĩa tiếng Việt.
/// Giá trị số lấy đúng theo hàm fn_generate_script_id của lược đồ.
/// </summary>
public enum ScriptRelation
{
    /// <summary>Từ tiếng Anh dịch thẳng được sang tiếng Việt: scan → quét.</summary>
    DirectTranslation = 1,

    /// <summary>Danh từ riêng, không dịch: Google Drive, Excel.</summary>
    ProperNoun = 2
}
