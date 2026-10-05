using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.WorkTasks;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// TaskProgressTracker là cầu nối giữa nộp/duyệt và tiến độ task — mock ITaskRepository
/// để kiểm từng nhánh mà không cần database.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TaskProgressTrackerTests
{
    private readonly Mock<ITaskRepository> _tasks = new(MockBehavior.Strict);
    private TaskProgressTracker Tracker => new(_tasks.Object);

    private static WorkTask Task(long id = 7, WorkTaskStatus status = WorkTaskStatus.Open, int target = 10) => new()
    {
        TaskId = id,
        CampaignId = 1,
        CreatedBy = 2,
        TaskType = TaskType.Recording,
        TargetQty = target,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static TaskRow Row(long id = 7, long? assignee = 9, int started = 1, int done = 0, int target = 10) => new(
        id, 1, "Chiến dịch", TaskType.Recording, null, WorkTaskStatus.Open, target,
        null, DateTimeOffset.UtcNow, assignee, "Người nhận", started, done, 1, 1);

    [Fact]
    public async Task RefreshStatus_DatChiTieu_ChuyenCompletedVaLuu()
    {
        // Arrange
        var task = Task(status: WorkTaskStatus.InProgress);
        _tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.GetRowAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row(done: 10, target: 10));
        _tasks.Setup(t => t.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        await Tracker.RefreshStatusAsync(7, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WorkTaskStatus.Completed, task.Status);
        _tasks.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshStatus_ChuaDatChiTieu_KhongLuu()
    {
        // Arrange
        var task = Task(status: WorkTaskStatus.Open);
        _tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.GetRowAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row(started: 0, done: 0));

        // Act
        await Tracker.RefreshStatusAsync(7, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WorkTaskStatus.Open, task.Status);
        _tasks.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshStatus_TaskKhongTonTai_ImLangBoQua()
    {
        // Arrange
        _tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((WorkTask?)null);
        _tasks.Setup(t => t.GetRowAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((TaskRow?)null);

        // Act
        await Tracker.RefreshStatusAsync(7, TestContext.Current.CancellationToken);

        // Assert
        _tasks.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnScriptRejected_KhongConMucCho_ImLangBoQua()
    {
        // Arrange
        _tasks.Setup(t => t.GetPendingScriptItemsAsync("s_111000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await Tracker.OnScriptRejectedAsync("s_111000001", TestContext.Current.CancellationToken);

        // Assert
        _tasks.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnReviewSubmitted_BanGhiConCho_ChiLamMoiMotTask()
    {
        // Arrange — task Open chưa có việc (started=0) nên RefreshStatus không đổi trạng thái, không lưu thêm.
        var recording = new Recording { RecordingId = "r_cs_111000001", ScriptId = "s_111000001" };
        var task = Task(status: WorkTaskStatus.Open);
        _tasks.Setup(t => t.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.GetRowAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(Row(started: 0, done: 0));

        // Act — finalStatus còn chờ: không đụng tới hàng đợi, chỉ refresh task gọi tới.
        await Tracker.OnReviewSubmittedAsync(recording, RecordingStatus.PendingReview, 7, TestContext.Current.CancellationToken);

        // Assert
        _tasks.Verify(t => t.GetQueuedRecordingItemsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _tasks.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnReviewSubmitted_BanGhiDaChotDat_VaDuHaiBan_DanhDauXong()
    {
        // Arrange
        var recording = new Recording { RecordingId = "r_cs_111000001", ScriptId = "s_111000001" };
        var queued = new TaskRecording { TaskId = 8, RecordingId = recording.RecordingId, Status = TaskRecordingStatus.Queued };
        var scriptItem = new TaskScript { TaskId = 7, ScriptId = recording.ScriptId, Status = TaskScriptStatus.Pending };
        var task7 = Task(id: 7, status: WorkTaskStatus.Open);
        var task8 = Task(id: 8, status: WorkTaskStatus.Open);

        _tasks.Setup(t => t.GetQueuedRecordingItemsAsync(recording.RecordingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([queued]);
        _tasks.Setup(t => t.IsScriptFullyApprovedAsync(recording.ScriptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tasks.Setup(t => t.GetPendingScriptItemsAsync(recording.ScriptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([scriptItem]);
        _tasks.Setup(t => t.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(task7);
        _tasks.Setup(t => t.GetForUpdateAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(task8);
        _tasks.Setup(t => t.GetRowAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(Row());

        // Act
        await Tracker.OnReviewSubmittedAsync(recording, RecordingStatus.Approved, 7, TestContext.Current.CancellationToken);

        // Assert — mục chờ bị bỏ qua, mục câu xong, cả hai task đều được refresh.
        Assert.Equal(TaskRecordingStatus.Skipped, queued.Status);
        Assert.Equal(TaskScriptStatus.Completed, scriptItem.Status);
        _tasks.Verify(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        _tasks.Verify(t => t.GetForUpdateAsync(8, It.IsAny<CancellationToken>()), Times.Once);
    }
}
