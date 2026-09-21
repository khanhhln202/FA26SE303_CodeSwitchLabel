using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.WorkTasks;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Trạng thái task được tính lại từ số liệu thật sau mỗi việc xảy ra. Tính sai thì task hiện
/// "Hoàn thành" khi chưa xong, hoặc kẹt ở "Nháp" dù đã giao — nên mỗi nhánh có test riêng.
/// </summary>
public class TaskStateMachineTests
{
    [Theory]
    [InlineData(false, 0, 0, 10)]
    [InlineData(true, 3, 1, 10)]
    [InlineData(true, 12, 10, 10)]
    public void DaHuy_KhongBaoGioTuMoLai_KeCaKhiDatChiTieu(bool hasAssignee, int started, int done, int target)
    {
        Assert.Equal(
            WorkTaskStatus.Cancelled,
            TaskStateMachine.Evaluate(WorkTaskStatus.Cancelled, hasAssignee, started, done, target));
    }

    [Fact]
    public void ChuaGiaoChoAi_LaNhap()
    {
        Assert.Equal(WorkTaskStatus.Draft, TaskStateMachine.Evaluate(WorkTaskStatus.Draft, false, 0, 0, 10));
    }

    [Fact]
    public void DaGiao_ChuaLamGi_LaMo()
    {
        Assert.Equal(WorkTaskStatus.Open, TaskStateMachine.Evaluate(WorkTaskStatus.Draft, true, 0, 0, 10));
    }

    [Fact]
    public void DaGiao_DaNopViecDauTien_LaDangLam()
    {
        Assert.Equal(WorkTaskStatus.InProgress, TaskStateMachine.Evaluate(WorkTaskStatus.Open, true, 1, 0, 10));
    }

    /// <summary>
    /// Nộp nhiều mà chưa được duyệt đạt thì chưa xong — đúng lựa chọn "xong khi được duyệt đạt".
    /// </summary>
    [Fact]
    public void NopDuChiTieu_NhungChuaDuDat_ChuaHoanThanh()
    {
        Assert.Equal(WorkTaskStatus.InProgress, TaskStateMachine.Evaluate(WorkTaskStatus.InProgress, true, 10, 7, 10));
    }

    /// <summary>Vượt chỉ tiêu vẫn là hoàn thành — các bản còn chờ duyệt được duyệt nốt sau khi task đã xong.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(13)]
    public void DatHoacVuotChiTieu_HoanThanh(int done)
    {
        Assert.Equal(WorkTaskStatus.Completed, TaskStateMachine.Evaluate(WorkTaskStatus.InProgress, true, 15, done, 10));
    }

    /// <summary>Completed không phải trạng thái cuối: Task Manager nâng chỉ tiêu thì task tự mở lại.</summary>
    [Fact]
    public void DaHoanThanh_NangChiTieu_TuMoLai()
    {
        Assert.Equal(WorkTaskStatus.InProgress, TaskStateMachine.Evaluate(WorkTaskStatus.Completed, true, 10, 10, 20));
    }

    /// <summary>Giao lại cho người khác giữa chừng: người mới chưa làm gì nhưng task đã có việc thật.</summary>
    [Fact]
    public void GiaoLai_TaskDaCoViec_VanDangLam()
    {
        Assert.Equal(WorkTaskStatus.InProgress, TaskStateMachine.Evaluate(WorkTaskStatus.InProgress, true, 4, 2, 10));
    }

    [Theory]
    [InlineData(WorkTaskStatus.Draft, false)]
    [InlineData(WorkTaskStatus.Open, true)]
    [InlineData(WorkTaskStatus.InProgress, true)]
    [InlineData(WorkTaskStatus.Completed, false)]
    [InlineData(WorkTaskStatus.Cancelled, false)]
    public void ChiTaskMoHoacDangLam_MoiNhanViec(WorkTaskStatus status, bool expected)
    {
        Assert.Equal(expected, TaskStateMachine.AcceptsWork(status));
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 8, 13)]
    [InlineData(10, 10, 100)]
    [InlineData(13, 10, 100)]
    [InlineData(5, 0, 0)]
    public void PhanTram_LamTronRaXa0_ChanTren100(int done, int target, int expected)
    {
        // 1/8 = 12.5%: làm tròn kiểu ngân hàng mặc định của Math.Round sẽ ra 12.
        Assert.Equal(expected, TaskStateMachine.Percent(done, target));
    }
}
