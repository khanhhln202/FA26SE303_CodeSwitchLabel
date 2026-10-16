namespace CodeSwitchLabel.Repositories.Enums;

// Mỗi enum dưới đây ứng với một kiểu ENUM thật trong docs/codeswitchlabel.sql.
// Tên kiểu và tên giá trị dịch sang snake_case: ItTechnology -> it_technology.
// Vì database lưu bằng TÊN chứ không phải số, thứ tự khai báo ở đây không quan trọng —
// nhưng đổi tên một giá trị là hỏng dữ liệu cũ.

/// <summary>
/// Trạng thái tài khoản. Suspended = khoá tạm vì dữ liệu kém chất lượng,
/// khác Inactive là tài khoản đã ngừng dùng. Ngưỡng nằm trong system_config (quality.*).
/// </summary>
public enum UserStatus { Active, Inactive, Suspended }

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

/// <summary>
/// TEAM_010: Một câu đi hai chặng — duyệt nội dung rồi mới ghi âm. Cả hai chặng dùng
/// chung một bảng script_queue để thứ tự FIFO và cơ chế giữ chỗ chỉ phải viết một lần.
/// </summary>
public enum QueueStage { TextReview, Recording }

/// <summary>
/// Claimed là trạng thái tạm có chủ trước khi hết hạn (claim_expires_at), nên câu bị
/// bỏ ngang quay lại hàng đợi thay vì kẹt vĩnh viễn.
/// </summary>
public enum QueueState { Queued, Claimed, Done, Dropped }

/// <summary>
/// TEAM_015: loại đề xuất của Speaker trên một câu đã duyệt nội dung.
/// Report = chỉ báo câu có lỗi (kèm lý do); Edit = gửi kèm bản sửa đề xuất.
/// </summary>
public enum ProposalKind { Report, Edit }

/// <summary>
/// TEAM_015: Pending = đang nằm trong hàng chờ của Reviewer; Accepted/Rejected = đã có
/// quyết định. Đề xuất Pending giữ câu khỏi hàng đợi ghi âm (trigger fn_script_proposal_sync);
/// xử lý xong câu tự quay lại hàng đợi.
/// </summary>
public enum ProposalStatus { Pending, Accepted, Rejected }

/// <summary>
/// Quan hệ giữa từ tiếng Anh và từ tiếng Việt tương ứng, lưu theo từng từ trong script_word.
/// proper_noun = danh từ riêng giữ nguyên, không dịch, nên vi_word phải bằng en_word.
/// </summary>
public enum ScriptWordRelation { SemanticEquivalent, ProperNoun }

public enum TaskType { Recording, Review }

/// <summary>
/// Ứng với kiểu task_status. Đặt tên WorkTaskStatus để khỏi lẫn với
/// System.Threading.Tasks.TaskStatus, nên phải khai báo tên kiểu PostgreSQL một cách tường minh.
///
/// TEAM_013: còn đúng ba trạng thái. Trước đây có thêm `Draft` (chưa giao) và `Open`
/// (đã giao mà chưa làm), nhưng hai trạng thái đó chỉ nói về việc giao chứ không nói về
/// việc làm — Task Manager nhìn bảng thì thấy y như nhau. Bỏ đi cho đúng ba.
/// </summary>
public enum WorkTaskStatus { InProgress, Completed, Cancelled }

/// <summary>
/// Ứng với kiểu campaign_status. Một chiến dịch là đơn vị kế hoạch của Task Manager
/// cho một đợt thu thập.
///
/// TEAM_013: trạng thái KHÔNG bấm tay — nó là hàm của các task bên trong đợt, xem
/// <c>CampaignStateMachine</c>. Không còn `Draft` (đợt mới tạo ra `Open`) và
/// `Cancelled` (không có đường huỷ cả đợt).
/// </summary>
public enum CampaignStatus { Open, InProgress, Completed }

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
