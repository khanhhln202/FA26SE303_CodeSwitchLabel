using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.WorkTasks;
using CodeSwitchLabel.Services.WorkTasks;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Trạng thái task được tính lại từ số liệu thật sau mỗi việc xảy ra. Tính sai thì task hiện
/// "Hoàn thành" khi chưa xong — nên mỗi nhánh có test riêng.
///
/// bỏ `Draft` và `Open`, nên luật còn đúng ba nhánh: đã huỷ thì giữ, đạt chỉ tiêu
/// thì hoàn thành, còn lại thì đang thực hiện.
/// </summary>
[Trait("Category", "Unit")]
public class TaskStateMachineTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(3, 10)]
    [InlineData(10, 10)]
    public void DaHuy_KhongBaoGioTuMoLai_KeCaKhiDatChiTieu(int done, int target)
    {
        // Arrange + Act
        var status = TaskStateMachine.Evaluate(WorkTaskStatus.Cancelled, done, target);

        // Assert
        Assert.Equal(WorkTaskStatus.Cancelled, status);
    }

    [Theory]
    [InlineData(0, 0, 10, WorkTaskStatus.InProgress)]
    [InlineData(0, 1, 10, WorkTaskStatus.InProgress)]
    [InlineData(1, 7, 10, WorkTaskStatus.InProgress)]
    [InlineData(1, 2, 10, WorkTaskStatus.InProgress)]
    [InlineData(1, 10, 10, WorkTaskStatus.Completed)]
    public void ChuyenTrangThai_ChuaDatChiTieuThiDangThucHien_DatThiHoanThanh(
        WorkTaskStatus current, int done, int target, WorkTaskStatus expected)
    {
        // Arrange + Act
        var status = TaskStateMachine.Evaluate(current, done, target);

        // Assert
        Assert.Equal(expected, status);
    }

    /// <summary>
    /// không còn nhánh "chưa giao" và "đã giao mà chưa làm", nên mọi task chưa đạt
    /// chỉ tiêu đều là một trạng thái — kể cả task vừa tạo ra chưa gán ai, và kể cả task
    /// Task Manager vừa mở lại bằng tay.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    public void ChuaDatChiTieu_DuCoBaoNhieuNguoiLam_DeuLaDangThucHien(int done)
    {
        // Arrange + Act — số người nhận không còn là đầu vào của luật trạng thái nữa
        var status = TaskStateMachine.Evaluate(WorkTaskStatus.InProgress, done, target: 10);

        // Assert
        Assert.Equal(WorkTaskStatus.InProgress, status);
    }

    /// <summary>Vượt chỉ tiêu vẫn là hoàn thành — các bản còn chờ duyệt được duyệt nốt sau khi task đã xong.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(13)]
    public void DatHoacVuotChiTieu_HoanThanh(int done)
    {
        // Arrange + Act
        var status = TaskStateMachine.Evaluate(WorkTaskStatus.InProgress, done, 10);

        // Assert
        Assert.Equal(WorkTaskStatus.Completed, status);
    }

    /// <summary>Completed không phải trạng thái cuối: Task Manager nâng chỉ tiêu thì task tự mở lại.</summary>
    [Fact]
    public void DaHoanThanh_NangChiTieu_TuMoLai()
    {
        // Arrange + Act
        var status = TaskStateMachine.Evaluate(WorkTaskStatus.Completed, done: 10, target: 20);

        // Assert
        Assert.Equal(WorkTaskStatus.InProgress, status);
    }

    /// <summary>
    /// TEAM_010 (yêu cầu 4.2): bản ghi bị từ chối làm chỉ tiêu tụt xuống dưới target —
    /// task phải tự rời Completed để người nhận thu bù.
    /// </summary>
    [Fact]
    public void ChiTieuTut_ChuyenTuCompletedVeInProgress()
    {
        // Arrange — trước đó 100/100, giờ còn 95/100 sau khi bản ghi bị từ chối
        var status = TaskStateMachine.Evaluate(WorkTaskStatus.Completed, done: 95, target: 100);

        // Assert
        Assert.Equal(WorkTaskStatus.InProgress, status);
    }

    /// <summary>
    /// Task Manager bấm tay chốt Completed, rồi có việc mới nộp vào làm chỉ tiêu
    /// tụt — hệ thống ghi đè về InProgress. Đây là điều user chốt: bấm tay giữ tới lúc có
    /// việc thật xảy ra, không phải vĩnh viễn.
    /// </summary>
    [Fact]
    public void TaskManagerChotHoanThanh_ChiTieuTut_HeThongMoLai()
    {
        // Arrange + Act
        var status = TaskStateMachine.Evaluate(WorkTaskStatus.Completed, done: 95, target: 100);

        // Assert
        Assert.Equal(WorkTaskStatus.InProgress, status);
    }

    [Theory]
    [InlineData(WorkTaskStatus.InProgress, true)]
    [InlineData(WorkTaskStatus.Completed, false)]
    [InlineData(WorkTaskStatus.Cancelled, false)]
    public void ChiTaskDangThucHien_MoiNhanViec(WorkTaskStatus status, bool expected)
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
        var evaluated = TaskStateMachine.Evaluate(unknown, done: 0, target: 10);
        var accepts = TaskStateMachine.AcceptsWork(unknown);

        // Assert
        Assert.Equal(WorkTaskStatus.InProgress, evaluated);
        Assert.False(accepts);
    }
}

/// <summary>
/// Trạng thái đợt là HÀM của các nhiệm vụ bên trong, không ai bấm tay được. Sai ở đây thì bảng
/// đợt hiện sai chữ ngay, nên từng nhánh có một test.
/// </summary>
[Trait("Category", "Unit")]
public class CampaignStateMachineTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 5)]
    public void DoTKhongCoNhiemVu_ThiDangMo(int taskCount, int completedTaskCount)
    {
        // Arrange + Act
        var status = CampaignStateMachine.Evaluate(taskCount, completedTaskCount);

        // Assert
        Assert.Equal(CampaignStatus.Open, status);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(5, 4)]
    public void CoNhiemVu_ChuaXongHet_ThiDangThucHien(int taskCount, int completedTaskCount)
    {
        // Arrange + Act
        var status = CampaignStateMachine.Evaluate(taskCount, completedTaskCount);

        // Assert
        Assert.Equal(CampaignStatus.InProgress, status);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(10, 10)]
    public void MoiNhiemVuDeuXong_ThiHoanThanh(int taskCount, int completedTaskCount)
    {
        // Arrange + Act
        var status = CampaignStateMachine.Evaluate(taskCount, completedTaskCount);

        // Assert
        Assert.Equal(CampaignStatus.Completed, status);
    }

    /// <summary>
    /// task đã huỷ KHÔNG tính là hoàn thành. Đợt có một task xong và một task bị
    /// huỷ thì vẫn là "Đang thực hiện" — đó là điều user chốt: "tất cả nhiệm vụ của đợt đó
    /// có trạng thái hoàn thành". Hệ quả là đợt không bao giờ tự Completed nếu còn task huỷ.
    /// </summary>
    [Fact]
    public void ConNhiemVuDaHuy_ThiDangVanChuaHoanThanh()
    {
        // Arrange — 2 task: 1 xong, 1 đã huỷ → completedTaskCount = 1, taskCount = 2
        var taskCount = 2;
        var completedTaskCount = 1;

        // Act
        var status = CampaignStateMachine.Evaluate(taskCount, completedTaskCount);

        // Assert
        Assert.Equal(CampaignStatus.InProgress, status);
    }

    /// <summary>completedTaskCount lớn hơn taskCount là dữ liệu hỏng, không phải lỗi của hàm.</summary>
    [Fact]
    public void DemNhieuHonTongSoTask_ThiVanDangThucHien()
    {
        // Arrange + Act
        var status = CampaignStateMachine.Evaluate(taskCount: 2, completedTaskCount: 5);

        // Assert
        Assert.Equal(CampaignStatus.InProgress, status);
    }
}