using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.WorkTasks;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Trạng thái task được tính lại từ số liệu thật sau mỗi việc xảy ra. Tính sai thì task hiện
/// "Hoàn thành" khi chưa xong, hoặc kẹt ở "Nháp" dù đã giao — nên mỗi nhánh có test riêng.
/// </summary>
[Trait("Category", "Unit")]
public class TaskStateMachineTests
{
    [Theory]
    [InlineData(false, 0, 0, 10)]
    [InlineData(true, 3, 1, 10)]
    [InlineData(true, 12, 10, 10)]
    public void DaHuy_KhongBaoGioTuMoLai_KeCaKhiDatChiTieu(bool hasAssignee, int started, int done, int target)
    {
        // Arrange + Act
        var status = TaskStateMachine.Evaluate(WorkTaskStatus.Cancelled, hasAssignee, started, done, target);

        // Assert
        Assert.Equal(WorkTaskStatus.Cancelled, status);
    }

    [Theory]
    [InlineData(WorkTaskStatus.Draft, false, 0, 0, 10, WorkTaskStatus.Draft)]
    [InlineData(WorkTaskStatus.Draft, true, 0, 0, 10, WorkTaskStatus.Open)]
    [InlineData(WorkTaskStatus.Open, true, 1, 0, 10, WorkTaskStatus.InProgress)]
    [InlineData(WorkTaskStatus.InProgress, true, 10, 7, 10, WorkTaskStatus.InProgress)]
    [InlineData(WorkTaskStatus.InProgress, true, 4, 2, 10, WorkTaskStatus.InProgress)]
    public void ChuyenTrangThai_CoBan_TheoNguoiNhanVaTienDo(
        WorkTaskStatus current, bool hasAssignee, int started, int done, int target, WorkTaskStatus expected)
    {
        // Arrange + Act
        var status = TaskStateMachine.Evaluate(current, hasAssignee, started, done, target);

        // Assert
        Assert.Equal(expected, status);
    }

    /// <summary>Vượt chỉ tiêu vẫn là hoàn thành — các bản còn chờ duyệt được duyệt nốt sau khi task đã xong.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(13)]
    public void DatHoacVuotChiTieu_HoanThanh(int done)
    {
        // Arrange + Act
        var status = TaskStateMachine.Evaluate(WorkTaskStatus.InProgress, true, 15, done, 10);

        // Assert
        Assert.Equal(WorkTaskStatus.Completed, status);
    }

    /// <summary>Completed không phải trạng thái cuối: Task Manager nâng chỉ tiêu thì task tự mở lại.</summary>
    [Fact]
    public void DaHoanThanh_NangChiTieu_TuMoLai()
    {
        // Arrange + Act
        var status = TaskStateMachine.Evaluate(WorkTaskStatus.Completed, true, 10, 10, 20);

        // Assert
        Assert.Equal(WorkTaskStatus.InProgress, status);
    }

    [Theory]
    [InlineData(WorkTaskStatus.Draft, false)]
    [InlineData(WorkTaskStatus.Open, true)]
    [InlineData(WorkTaskStatus.InProgress, true)]
    [InlineData(WorkTaskStatus.Completed, false)]
    [InlineData(WorkTaskStatus.Cancelled, false)]
    public void ChiTaskMoHoacDangLam_MoiNhanViec(WorkTaskStatus status, bool expected)
    {
        // Arrange + Act
        var accepts = TaskStateMachine.AcceptsWork(status);

        // Assert
        Assert.Equal(expected, accepts);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 8, 13)]
    [InlineData(10, 10, 100)]
    [InlineData(13, 10, 100)]
    [InlineData(5, 0, 0)]
    [InlineData(-3, 10, -30)] // số âm: hàm không chặn, service phải chặn trước khi gọi
    [InlineData(5, -10, 0)] // chỉ tiêu âm coi như không có chỉ tiêu
    public void PhanTram_LamTronRaXa0_ChanTren100(int done, int target, int expected)
    {
        // Arrange — 1/8 = 12.5%: làm tròn kiểu ngân hàng mặc định của Math.Round sẽ ra 12.
        // Act
        var percent = TaskStateMachine.Percent(done, target);

        // Assert
        Assert.Equal(expected, percent);
    }

    [Fact]
    public void TrangThaiLa_GiaTriKhongXacDinh_RoiVaoNhanhMacDinh()
    {
        // Arrange — phòng khi enum thêm giá trị mới mà state machine chưa biết.
        var unknown = (WorkTaskStatus)999;

        // Act
        var evaluated = TaskStateMachine.Evaluate(unknown, hasActiveAssignee: true, started: 0, done: 0, target: 10);
        var accepts = TaskStateMachine.AcceptsWork(unknown);

        // Assert
        Assert.Equal(WorkTaskStatus.Open, evaluated);
        Assert.False(accepts);
    }
}
