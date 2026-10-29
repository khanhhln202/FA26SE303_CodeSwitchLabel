using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Repositories.WorkTasks;

/// <summary>
/// Luật trạng thái đợt (chiến dịch), viết thành hàm thuần để test được từng nhánh.
///
///   Chưa có nhiệm vụ nào  → Open
///   Có nhiệm vụ           → InProgress
///   Mọi nhiệm vụ đã xong  → Completed
///
/// trước đây đợt có `Draft`/`Open`/`InProgress`/`Completed`/`Cancelled` nhưng
/// `wave.status` gần như không bao giờ được cập nhật — tạo ra là `Draft`, rồi chỉ đổi
/// khi Admin PATCH tay, nên bảng đợt trên frontend phải tự đếm task mới hiện được chữ
/// "Hoàn thành". Giờ trạng thái đợt là HÀM của task, không ai bấm tay nữa.
///
/// Hàm này nằm ở tầng Repositories chứ không phải Services vì
/// <c>WaveRepository.RefreshStatusAsync</c> gọi nó — mà Repositories không tham chiếu Services.
///
/// Task `Cancelled` KHÔNG tính là hoàn thành — đợt còn task đã huỷ thì vẫn `InProgress`.
/// Đó là điều user chốt: "tất cả nhiệm vụ của đợt đó có trạng thái hoàn thành".
/// </summary>
public static class WaveStateMachine
{
    public static WaveStatus Evaluate(int taskCount, int completedTaskCount) =>
        taskCount == 0
            ? WaveStatus.Open
            : completedTaskCount == taskCount
                ? WaveStatus.Completed
                : WaveStatus.InProgress;
}