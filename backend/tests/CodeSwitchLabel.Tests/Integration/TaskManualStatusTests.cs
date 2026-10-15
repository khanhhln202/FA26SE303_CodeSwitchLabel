using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// TEAM_013: chọn `Cancelled` bằng tay phải y hệt nút Huỷ — lượt giao đang hoạt động
/// chuyển sang `cancelled`.
///
/// Hai đường ghi trạng thái là hai chỗ khác nhau trong code thì chỗ này sẽ lệch chỗ kia:
/// lượt giao còn `active` thì người nhận vẫn thấy task trong danh sách của mình, và
/// task đã dừng lại thì vẫn giữ chỗ cho họ. Vì vậy cả hai đường gọi chung một hàm.
///
/// Test này dựng lượt giao thẳng bằng Db chứ không qua API: `POST /assign` kiểm tra
/// chỉ tiêu phải nhỏ hơn số mục, mà tạo mục qua HTTP thì cần cả đường nhập câu rồi duyệt.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class TaskManualStatusTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task ChonCancelledTay_KeoTheoLượtGiaoDangHoatDong()
    {
        // Arrange
        var (task, _) = await TaoTaskCoNguoiNhanAsync();

        // Act
        var detail = await TaskService.UpdateAsync(
            task.TaskId, new UpdateTaskRequest { Status = WorkTaskStatus.Cancelled },
            TestContext.Current.CancellationToken, task.CreatedBy);

        // Assert
        Assert.Equal(WorkTaskStatus.Cancelled, detail.Summary.Status);
        Assert.All(detail.Assignments,
            a => Assert.Equal(AssignmentStatus.Cancelled, a.Status));
    }

    /// <summary>
    /// Đường bấm tay và nút Huỷ phải cho ra CÙNG một kết quả. Nếu sau này có ai sửa
    /// một trong hai chỗ, test này đỏ ngay thay vì để hai luật lệch nhau trên production.
    /// </summary>
    [Fact]
    public async Task ChonCancelledTay_GiongHetNutHuy()
    {
        // Arrange
        var (task, _) = await TaoTaskCoNguoiNhanAsync();

        // Act — cùng một kết quả mong đợi với nhánh CancelAsync
        var detail = await TaskService.UpdateAsync(
            task.TaskId, new UpdateTaskRequest { Status = WorkTaskStatus.Cancelled },
            TestContext.Current.CancellationToken, task.CreatedBy);

        // Assert — mục công việc giữ nguyên để còn dấu vết, chỉ lượt giao bị huỷ.
        Assert.Equal(WorkTaskStatus.Cancelled, detail.Summary.Status);
        Assert.DoesNotContain(detail.Assignments, a => a.Status == AssignmentStatus.Active);
    }

    /// <summary>Bấm tay Hoàn thành thì lượt giao giữ nguyên — task xong không có nghĩa ngừng người làm.</summary>
    [Fact]
    public async Task ChonCompletedTay_KhongDungLượtGiao()
    {
        // Arrange
        var (task, _) = await TaoTaskCoNguoiNhanAsync();

        // Act
        var detail = await TaskService.UpdateAsync(
            task.TaskId, new UpdateTaskRequest { Status = WorkTaskStatus.Completed },
            TestContext.Current.CancellationToken, task.CreatedBy);

        // Assert
        Assert.Equal(WorkTaskStatus.Completed, detail.Summary.Status);
        Assert.All(detail.Assignments,
            a => Assert.Equal(AssignmentStatus.Active, a.Status));
    }

    // ----------------------------------------------------------------- dựng dữ liệu

    private async Task<(WorkTask Task, AppUser Speaker)> TaoTaskCoNguoiNhanAsync()
    {
        var manager = await CreateUserAsync($"tm-cancel-{Guid.NewGuid():N}@test.local", RoleName.TaskManager);
        var speaker = await CreateUserAsync($"sp-cancel-{Guid.NewGuid():N}@test.local", RoleName.Speaker);

        var campaign = await CreateCampaignAsync($"Đợt huỷ tay {Guid.NewGuid():N}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), manager.UserId, CampaignStatus.Open);

        campaign.AssignedTo = manager.UserId;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var task = await CreateTaskAsync(campaign.CampaignId, TaskType.Recording, 1,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = task.TaskId,
            UserId = speaker.UserId,
            AssignedAt = DateTimeOffset.UtcNow,
            AssignmentStatus = AssignmentStatus.Active
        });

        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (task, speaker);
    }
}