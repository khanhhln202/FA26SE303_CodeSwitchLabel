using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.WorkTasks;

/// <summary>
/// Luật trạng thái task, viết thành hàm thuần để test được từng nhánh.
///
///   Đã huỷ     → giữ nguyên, không bao giờ tự mở lại
///   Đạt chỉ tiêu → Completed
///   Còn lại     → InProgress
///
/// Trạng thái được TÍNH LẠI từ số liệu thật mỗi khi có việc xảy ra — nộp bản ghi, duyệt,
/// bỏ qua, giao lại, sửa chỉ tiêu — thay vì cộng trừ từng bước. Tính lại thì không bao giờ lệch.
///
/// TEAM_013: bỏ `Draft` và `Open`. Trước đây còn phân biệt "chưa giao" với "đã giao mà chưa
/// làm" nhưng cả hai đều là việc đang dở, và Task Manager bấm tay cũng chỉ đổi được sang
/// hai trạng thái đó nên luật tự chạy và luật bấm tay lệch nhau. Giờ còn một nhánh duy nhất.
///
/// Completed không phải trạng thái cuối: Task Manager nâng chỉ tiêu thì task tự mở lại.
/// Nhánh "chưa đạt chỉ tiêu" cũng tự mở lại được: bản ghi bị từ chối làm chỉ tiêu tụt,
/// task rơi về InProgress và người nhận phải thu bù (yêu cầu 4.2).
///
/// TEAM_016: Completed KHÔNG khoá công việc — người nhận còn lượt giao Active thì thu/duyệt
/// tiếp được và số vượt chỉ tiêu vẫn ghi nhận; chỉ Cancelled mới chặn. KPI để đo, không để khoá.
///
/// Lưu ý: trạng thái Task Manager bấm tay KHÔNG bị nhánh này ghi đè ngay lập tức —
/// nó được giữ tới khi có việc thật xảy ra. Xem `TaskService.UpdateAsync`.
/// </summary>
public static class TaskStateMachine
{
    public static WorkTaskStatus Evaluate(WorkTaskStatus current, int done, int target)
    {
        if (current == WorkTaskStatus.Cancelled) return WorkTaskStatus.Cancelled;
        if (target > 0 && done >= target) return WorkTaskStatus.Completed;

        return WorkTaskStatus.InProgress;
    }

    /// <summary>
    /// Dùng cho cờ "quá hạn": chỉ task CHƯA đạt chỉ tiêu mới bị coi là đang làm dở quá hạn.
    /// Việc nhận thêm bản ghi/lượt duyệt thì rộng hơn — task Completed vẫn nhận, chỉ
    /// Cancelled mới chặn (các truy vấn trong repository, TEAM_016).
    /// </summary>
    public static bool AcceptsWork(WorkTaskStatus status) => status == WorkTaskStatus.InProgress;

    /// <summary>
    /// Làm tròn, chặn trên 100. Backend tính sẵn để mọi màn hình ra cùng một con số.
    /// Ghim AwayFromZero vì Math.Round mặc định làm tròn kiểu ngân hàng: 12.5 ra 12 chứ không ra 13,
    /// lệch với Math.round bên JavaScript.
    /// </summary>
    public static int Percent(int done, int target) =>
        target <= 0 ? 0 : (int)Math.Min(100, Math.Round(100.0 * done / target, MidpointRounding.AwayFromZero));
}