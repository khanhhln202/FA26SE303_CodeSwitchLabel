namespace CodeSwitchLabel.Repositories.Enums;

// TEAM_012: cho phép client chọn thứ tự của mọi endpoint trả danh sách phân trang
// bằng `?sort=<cột>&dir=asc|desc`.
//
// Nguyên tắc: KHÔNG gửi `sort` thì ra đúng thứ tự cũ. Nhờ vậy client cũ không phải
// đổi gì, và tên cột gõ sai chỉ bị bỏ qua chứ không làm hỏng request.

/// <summary>
/// Yêu cầu sắp xếp của client cho một danh sách, đã đọc từ `?sort=` và `?dir=`.
///
/// <para><c>Field = null</c> nghĩa là "dùng thứ tự mặc định của endpoint". Đó cũng chính là
/// giá trị khi client không gửi gì, nên một yêu cầu hỏng không bao giờ khác một yêu cầu
/// không có tham số.</para>
///
/// <para>Repository đọc <c>Field</c> để chọn nhánh OrderBy. Mọi nhánh đều phải nối tiếp
/// khoá chính bằng ThenBy (xem từng repository): nếu cột sắp xếp trùng nhau, trang sau sẽ
/// lặp hoặc hụt dòng.</para>
/// </summary>
public readonly record struct SortSpec<TField>(TField? Field, bool Descending) where TField : struct, Enum
{
    /// <summary>Thứ tự mặc định của endpoint — cũng là đáp án cho mọi tên cột không hợp lệ.</summary>
    public static SortSpec<TField> Default => new(null, false);

    /// <summary>
    /// Đọc `?sort=` và `?dir=`. Tên cột không khớp giá trị nào của enum thì rơi về
    /// <see cref="Default"/> thay vì ném lỗi — đúng như cách endpoint đang bỏ qua
    /// `status`/`kind` sai kiểu ở ReviewerStatsService.
    /// </summary>
    public static SortSpec<TField> From(string? sort, string? dir)
    {
        var name = sort?.Trim();

        if (!string.IsNullOrEmpty(name) && Enum.TryParse<TField>(name, ignoreCase: true, out var field))
        {
            // `dir` thiếu = tăng dần. Chỉ "desc" mới lật chiều, còn lại (kể cả rác) coi như tăng dần.
            return new SortSpec<TField>(
                field, string.Equals(dir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase));
        }

        return Default;
    }
}

// Mỗi endpoint có một enum riêng vì mỗi bảng có cột riêng: đổi danh sách nào hỗ trợ cột nào
// là chuyện của từng endpoint, không phải một bảng tra chung cho cả hệ thống.
// Tên giá trị viết camelCase vì đó là đúng thứ client gửi lên (`?sort=createdAt`).

/// <summary>Kho câu: GET /api/scripts, GET /api/speaker/contributions/history.</summary>
public enum ScriptSortField { ScriptId, CreatedAt, Domain, Status }

/// <summary>Bản ghi: GET /api/recordings.</summary>
public enum RecordingSortField { RecordingId, RecordedAt, Status, ScriptId, DurationSec }

/// <summary>Danh sách bản ghi trong một task duyệt: GET /api/reviewer/tasks/{id}/recordings.</summary>
public enum TaskReviewSortField { RecordedAt, Status, ReviewCount, ScriptId }

/// <summary>Nhiệm vụ: GET /api/tasks.</summary>
public enum TaskSortField { TaskId, Deadline, Status, TaskType, CreatedAt, TargetQty }

/// <summary>Tài khoản: GET /api/users.</summary>
public enum UserSortField { UserId, FullName, Email, Status, Role, CreatedAt }

/// <summary>Chiến dịch: GET /api/campaigns.</summary>
public enum CampaignSortField { CampaignId, StartDate, EndDate, Status, CreatedAt }

/// <summary>Bộ dữ liệu: GET /api/datasets.</summary>
public enum DatasetSortField { DatasetId, CreatedAt, Status, DatasetName, RecordingCount }

/// <summary>Lô nhập: GET /api/scripts/import/batches.</summary>
public enum ImportBatchSortField { BatchId, CreatedAt, FileName, ScriptCount }

/// <summary>Lịch sử duyệt bản ghi: GET /api/reviewer/history/recordings.</summary>
public enum ReviewSortField { ReviewedAt, Decision, Status, ScriptId }

/// <summary>Lịch sử duyệt câu: GET /api/reviewer/history/contributions.</summary>
public enum ScriptReviewSortField { ReviewedAt, Action, Status, Domain }

/// <summary>
/// Lịch sử ghi âm của Speaker: GET /api/speaker/recordings/history.
///
/// TEAM_012: một dòng là MỘT CẶP bản ghi, mà cặp không có cột riêng trong database —
/// nó được tính lúc truy vấn bằng GroupBy(ScriptId). Chỉ những cột nào biểu được bằng
/// biểu thức gom nhóm mới sắp xếp được TRƯỚC khi cắt trang; trạng thái và thời lượng của
/// cặp thì không, nên cố tình không có ở đây.
/// </summary>
public enum SpeakerPairSortField { LastRecordedAt, ScriptId }
