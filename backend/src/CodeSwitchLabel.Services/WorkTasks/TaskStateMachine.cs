using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.WorkTasks;

/// <summary>
/// Luật trạng thái task, viết thành hàm thuần để test được từng nhánh.
///
///   Đã huỷ                    → giữ nguyên, không bao giờ tự mở lại
///   Đạt chỉ tiêu              → Completed
///   Chưa giao cho ai          → Draft
///   Người nhận đã bắt tay làm → InProgress
///   Đã giao, chưa làm gì      → Open
///
/// Trạng thái được TÍNH LẠI từ số liệu thật mỗi khi có việc xảy ra — nộp bản ghi, duyệt,
/// bỏ qua, giao lại, sửa chỉ tiêu — thay vì cộng trừ từng bước. Tính lại thì không bao giờ lệch.
///
/// Completed không phải trạng thái cuối: Task Manager nâng chỉ tiêu thì task tự mở lại.
/// Nhánh "chưa ai nhận" cũng tự mở lại được: bản ghi bị từ chối làm chỉ tiêu tụt,
/// task rơi về InProgress và người nhận phải thu bù (yêu cầu 4.2).
/// </summary>
public static class TaskStateMachine
{
    /// <param name="activeAssigneeCount">
    /// TEAM_010: một task giao cho nhiều người, nên đếm số người chứ không phải kiểm
    /// "có hay không". Chỉ cần biết có người nào không (0 hay ít nhất 1).
    /// </param>
    public static WorkTaskStatus Evaluate(
        WorkTaskStatus current, int activeAssigneeCount, int started, int done, int target)
    {
        if (current == WorkTaskStatus.Cancelled) return WorkTaskStatus.Cancelled;
        if (target > 0 && done >= target) return WorkTaskStatus.Completed;
        if (activeAssigneeCount == 0) return WorkTaskStatus.Draft;

        return started > 0 ? WorkTaskStatus.InProgress : WorkTaskStatus.Open;
    }

    /// <summary>Chỉ hai trạng thái này mới nhận bản ghi hay lượt duyệt tính vào task.</summary>
    public static bool AcceptsWork(WorkTaskStatus status) =>
        status is WorkTaskStatus.Open or WorkTaskStatus.InProgress;

    /// <summary>
    /// Làm tròn, chặn trên 100. Backend tính sẵn để mọi màn hình ra cùng một con số.
    /// Ghim AwayFromZero vì Math.Round mặc định làm tròn kiểu ngân hàng: 12.5 ra 12 chứ không ra 13,
    /// lệch với Math.round bên JavaScript.
    /// </summary>
    public static int Percent(int done, int target) =>
        target <= 0 ? 0 : (int)Math.Min(100, Math.Round(100.0 * done / target, MidpointRounding.AwayFromZero));
}
