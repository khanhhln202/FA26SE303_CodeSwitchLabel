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

    // TEAM_013: tracker gọi tính lại trạng thái đợt mỗi khi trạng thái task đổi.
    private readonly Mock<ICampaignRepository> _campaigns = new(MockBehavior.Strict);
    private TaskProgressTracker Tracker => new(_tasks.Object, _campaigns.Object);

    private Mock<ICampaignRepository> GivenCampaigns()
    {
        _campaigns
            .Setup(c => c.RefreshStatusAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(global::System.Threading.Tasks.Task.CompletedTask);
        return _campaigns;
    }

    private static WorkTask Task(long id = 7, WorkTaskStatus status = WorkTaskStatus.InProgress, int target = 10) => new()
    {
        TaskId = id,
        CampaignId = 1,
        CreatedBy = 2,
        TaskType = TaskType.Recording,
        TargetQty = target,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static TaskRow Row(
        long id = 7, long? assignee = 9, int started = 1, int done = 0, int target = 10) => new(
        id, 1, "Đợt 1", TaskType.Recording, ScriptDomain.ItTechnology, null, WorkTaskStatus.InProgress, target,
        null, DateTimeOffset.UtcNow,
        assignee.HasValue ? [new TaskAssigneeRow(assignee.Value, "Người nhận")] : [],
        started, done, started, 1, 1);

    [Fact]
    public async Task RefreshStatus_DatChiTieu_ChuyenCompletedVaLuu()
    {
        // Arrange
        var task = Task(status: WorkTaskStatus.InProgress);
        GivenCampaigns();
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
        var task = Task(status: WorkTaskStatus.InProgress);
        _tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.GetRowAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row(started: 0, done: 0));

        // Act
        await Tracker.RefreshStatusAsync(7, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WorkTaskStatus.InProgress, task.Status);
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
        // Arrange — task chưa có việc (started=0) nhưng vẫn là InProgress nên không đổi trạng thái.
        var recording = new Recording { RecordingId = "r_cs_111000001", ScriptId = "s_111000001" };
        var task = Task(status: WorkTaskStatus.InProgress);
        GivenCampaigns();
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
        var task7 = Task(id: 7, status: WorkTaskStatus.InProgress);
        var task8 = Task(id: 8, status: WorkTaskStatus.InProgress);

        GivenCampaigns();
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

    /// <summary>
    /// TEAM_010 (yêu cầu 4.2) — BUG THẬT trước đây: câu đã tính xong rồi bản ghi bị
    /// từ chối thì mục vẫn giữ `Completed`, nên Done không giảm và task đứng ở
    /// Completed dù chỉ tiêu đã tụt. Người nhận không được biết phải thu bù.
    /// </summary>
    [Fact]
    public async Task OnReviewSubmitted_BanGhiTuChoi_LuiMucCauVaLuiMucDuyet()
    {
        // Arrange — task thu âm đã có 1 cặp câu xong, và task duyệt đã đếm 1 lượt.
        var recording = new Recording { RecordingId = "r_cs_111000001", ScriptId = "s_111000001" };

        var doneScriptItem = new TaskScript
        {
            TaskId = 7, ScriptId = recording.ScriptId, Status = TaskScriptStatus.Completed
        };
        var reviewedItem = new TaskRecording
        {
            TaskId = 8, RecordingId = recording.RecordingId, Status = TaskRecordingStatus.Reviewed
        };

        var task7 = Task(id: 7, status: WorkTaskStatus.Completed, target: 10);
        var task8 = Task(id: 8, status: WorkTaskStatus.Completed, target: 10);

        GivenCampaigns();
        _tasks.Setup(t => t.GetQueuedRecordingItemsAsync(recording.RecordingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _tasks.Setup(t => t.IsScriptFullyApprovedAsync(recording.ScriptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _tasks.Setup(t => t.GetCompletedScriptItemsAsync(recording.ScriptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([doneScriptItem]);
        _tasks.Setup(t => t.GetReviewedRecordingItemsAsync(recording.RecordingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([reviewedItem]);
        _tasks.Setup(t => t.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(task7);
        _tasks.Setup(t => t.GetForUpdateAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(task8);

        // Done tụt từ 10/10 xuống 9/10 sau khi trừ một câu.
        _tasks.Setup(t => t.GetRowAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row(started: 10, done: 9, target: 10));
        _tasks.Setup(t => t.GetRowAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row(started: 10, done: 9, target: 10));

        // Act
        await Tracker.OnReviewSubmittedAsync(recording, RecordingStatus.Rejected, 7, TestContext.Current.CancellationToken);

        // Assert — mục câu lùi về Pending, mục duyệt thành Skipped (không phải Queued:
        // bản ghi đã chốt rồi, không ai duyệt lại được), và cả hai task rời Completed.
        Assert.Equal(TaskScriptStatus.Pending, doneScriptItem.Status);
        Assert.Equal(TaskRecordingStatus.Skipped, reviewedItem.Status);
        Assert.Equal(WorkTaskStatus.InProgress, task7.Status);
        Assert.Equal(WorkTaskStatus.InProgress, task8.Status);
    }

    /// <summary>TEAM_010: khi bản ghi ĐẠT thì không được đụng vào mục đã xong — chỉ đường lùi mới lùi.</summary>
    [Fact]
    public async Task OnReviewSubmitted_BanGhiDat_KhongLuiMucNao()
    {
        // Arrange
        var recording = new Recording { RecordingId = "r_cs_111000001", ScriptId = "s_111000001" };
        var task = Task(id: 7, status: WorkTaskStatus.InProgress);

        GivenCampaigns();
        _tasks.Setup(t => t.GetQueuedRecordingItemsAsync(recording.RecordingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _tasks.Setup(t => t.IsScriptFullyApprovedAsync(recording.ScriptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tasks.Setup(t => t.GetPendingScriptItemsAsync(recording.ScriptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _tasks.Setup(t => t.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.GetRowAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row(started: 10, done: 10, target: 10));

        // Act
        await Tracker.OnReviewSubmittedAsync(recording, RecordingStatus.Approved, 7, TestContext.Current.CancellationToken);

        // Assert
        _tasks.Verify(
            t => t.GetCompletedScriptItemsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _tasks.Verify(
            t => t.GetReviewedRecordingItemsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(WorkTaskStatus.Completed, task.Status);
    }

    /// <summary>
    /// TEAM_010: task có nhiều người nhận thì RefreshStatus phải tính bằng SỐ người,
    /// không phải "có người hay không" — số người nhận không còn là đầu vào của luật trạng thái.
    ///
    /// TEAM_013: trạng thái đợt phải được tính lại khi trạng thái task đổi, và KHÔNG tính lại
    /// khi task giữ nguyên — nộp bản ghi xong trong lúc task vẫn InProgress là chuyện thường,
    /// mỗi lần lại khoá dòng campaign thì đợt nhiều task sẽ nghẽn hàng đợi.
    /// </summary>
    [Fact]
    public async Task RefreshStatus_TaskKhongDoiTrangThai_ThiDauKhongTinhLaiChienDich()
    {
        // Arrange — 2/10 nên vẫn là InProgress, y hệt trạng thái hiện tại
        var task = Task(status: WorkTaskStatus.InProgress);
        var campaigns = GivenCampaigns();
        _tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.GetRowAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row(started: 5, done: 2, target: 10));

        // Act
        await Tracker.RefreshStatusAsync(7, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WorkTaskStatus.InProgress, task.Status);
        campaigns.Verify(c => c.RefreshStatusAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _tasks.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshStatus_TaskDoiTrangThai_ThiTinhLaiTrangThaiChienDich()
    {
        // Arrange — 10/10 nên sang Completed
        var task = Task(status: WorkTaskStatus.InProgress);
        var campaigns = GivenCampaigns();
        _tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.GetRowAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row(done: 10, target: 10));
        _tasks.Setup(t => t.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        await Tracker.RefreshStatusAsync(7, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WorkTaskStatus.Completed, task.Status);
        campaigns.Verify(c => c.RefreshStatusAsync(task.CampaignId!.Value, It.IsAny<CancellationToken>()), Times.Once);
    }
}
