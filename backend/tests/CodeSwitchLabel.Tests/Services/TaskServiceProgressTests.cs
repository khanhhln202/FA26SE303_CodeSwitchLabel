using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Services.WorkTasks;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Task Manager đếm việc đã ĐƯỢC DUYỆT, Speaker đếm việc đã THU XONG — cùng một task
/// nhưng hai bộ số khác nhau. Test ở đây ghim đúng cái sai lệch đó, vì nó chỉ lộ ra
/// khi dữ liệu thật lệch nhau chứ lúc build không bắt được.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TaskServiceProgressTests
{
    private readonly Mock<ITaskRepository> _tasks = new(MockBehavior.Strict);
    private readonly TaskService _service;

    public TaskServiceProgressTests()
    {
        _service = new TaskService(
            _tasks.Object,
            Mock.Of<ICampaignRepository>(),
            Mock.Of<IScriptQueueRepository>(),
            Mock.Of<ITaskProgressTracker>(),
            Mock.Of<ISystemConfigService>(),
            TimeProvider.System);
    }

    /// <summary>Speaker vừa gửi đủ hai bản, chưa có bản nào được duyệt.</summary>
    private static TaskRow Row(int done = 0, int submittedPairs = 4, int target = 10) => new(
        7, 1, "Đợt 1", TaskType.Recording, ScriptDomain.ItTechnology, "Thu âm", WorkTaskStatus.InProgress, target,
        null, DateTimeOffset.UtcNow, [new TaskAssigneeRow(9, "Người đọc", target)],
        Started: 8, Done: done, SubmittedPairs: submittedPairs, UsableItems: 10, TotalItems: 10);

    [Fact]
    public async Task Speaker_TienDo_DemCapCauDaThuDu_ChuaCanChoDuyet()
    {
        _tasks.Setup(t => t.GetActiveRowsForAssigneeAsync(9, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Row()]);
        _tasks.Setup(t => t.GetSpeakerTotalsAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((8, 0));
        // riêng phần việc của người 9 trong task.
        _tasks.Setup(t => t.GetAssigneeSubmittedAsync(7, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, int> { [9] = 5, [10] = 6, [11] = 7 });
        _tasks.Setup(t => t.GetAssigneeDoneAsync(7, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, int> { [9] = 4 });

        var result = await _service.GetSpeakerProgressAsync(9, TestContext.Current.CancellationToken);

        var progress = Assert.Single(result.ActiveTasks).MyProgress;
        Assert.Equal(4, progress.Done);
        Assert.Equal(40, progress.Percent);
        Assert.Equal(8, result.TotalSubmitted);
    }

    [Fact]
    public async Task Speaker_TienDo_KhongVuotChiTieu()
    {
        // Cặp câu bị từ chối rồi thu lại thì cảm ứng chồng lên nhau — trên hiển thị vẫn không quá 100%.
        _tasks.Setup(t => t.GetActiveRowsForAssigneeAsync(9, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Row(submittedPairs: 10)]);
        _tasks.Setup(t => t.GetSpeakerTotalsAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((10, 0));
        _tasks.Setup(t => t.GetAssigneeSubmittedAsync(7, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, int> { [9] = 5, [10] = 6, [11] = 7 });
        _tasks.Setup(t => t.GetAssigneeDoneAsync(7, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, int> { [9] = 10 });

        var result = await _service.GetSpeakerProgressAsync(9, TestContext.Current.CancellationToken);

        var progress = Assert.Single(result.ActiveTasks).MyProgress;
        Assert.Equal(100, progress.Percent);
    }

    /// <summary>
    /// task có 3 người nhận thì mỗi người thấy TIẾN ĐỘ CỦA MÌNH, không
    /// phải tổng của cả nhóm. Trước đây cả ba thấy cùng một con số.
    /// </summary>
    [Fact]
    public async Task TaskNhieuNguoi_MoiNguoiThayTienDoRieng_ConTeamProgressChung()
    {
        var row = Row(done: 9, submittedPairs: 12, target: 30) with
        {
            Assignees =
            [
                new TaskAssigneeRow(9, "A", 10),
                new TaskAssigneeRow(10, "B", 10),
                new TaskAssigneeRow(11, "C", 10)
            ]
        };

        _tasks.Setup(t => t.GetActiveRowsForAssigneeAsync(9, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync([row]);
        _tasks.Setup(t => t.GetSpeakerTotalsAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((12, 9));
        _tasks.Setup(t => t.GetAssigneeSubmittedAsync(7, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, int> { [9] = 5, [10] = 6, [11] = 7 });
        _tasks.Setup(t => t.GetAssigneeDoneAsync(7, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, int> { [9] = 3, [10] = 4, [11] = 5 });

        var result = await _service.GetSpeakerProgressAsync(9, TestContext.Current.CancellationToken);

        var task = Assert.Single(result.ActiveTasks);

        // Riêng người 9: 3 cặp câu trên quota 10 → 30%.
        Assert.Equal(3, task.MyProgress.Done);
        Assert.Equal(10, task.MyProgress.TargetQty);
        Assert.Equal(30, task.MyProgress.Percent);

        // Cả nhóm: 12 cặp câu đã thu đủ trên tổng 30 → 40%.
        Assert.Equal(12, task.TeamProgress.Done);
        Assert.Equal(30, task.TeamProgress.TargetQty);
        Assert.Equal(40, task.TeamProgress.Percent);

        // Số đã NỘP cũng phải là của riêng người xem, không phải của cả task.
        // Đây là lỗi đội frontend báo: task ba người thì cả ba cùng thấy tổng của nhóm.
        Assert.Equal(5, task.MyProgress.Submitted);
        Assert.Equal(8, task.TeamProgress.Submitted);

        // Thấy rõ các người cùng nhận.
        Assert.Equal(3, task.Assignees.Count);
        Assert.Equal([9L, 10L, 11L], task.Assignees.Select(a => a.UserId).Order());
    }

    /// <summary>
    /// V12: quota nằm trên từng lượt giao, không phải lấy target của task chia đều
    /// lúc hiển thị. Người nhận quota 20/30 phải thấy 20, không phải 10.
    /// </summary>
    [Fact]
    public async Task Speaker_ThayQuotaRiengCuaMinh()
    {
        var row = Row(done: 3, submittedPairs: 12, target: 30) with
        {
            Assignees =
            [
                new TaskAssigneeRow(9, "A", 20),
                new TaskAssigneeRow(10, "B", 5),
                new TaskAssigneeRow(11, "C", 5)
            ]
        };

        _tasks.Setup(t => t.GetActiveRowsForAssigneeAsync(9, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync([row]);
        _tasks.Setup(t => t.GetSpeakerTotalsAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((12, 3));
        _tasks.Setup(t => t.GetAssigneeSubmittedAsync(7, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, int> { [9] = 5, [10] = 4, [11] = 3 });
        _tasks.Setup(t => t.GetAssigneeDoneAsync(7, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, int> { [9] = 3, [10] = 2, [11] = 1 });

        var result = await _service.GetSpeakerProgressAsync(9, TestContext.Current.CancellationToken);
        var task = Assert.Single(result.ActiveTasks);

        Assert.Equal(20, task.MyProgress.TargetQty);
        Assert.Equal(3, task.MyProgress.Done);
        Assert.Equal(15, task.MyProgress.Percent);
        Assert.Equal(30, task.TeamProgress.TargetQty);
    }

    [Fact]
    public async Task TaskManager_DanhSach_VanDemCapCauDaDuyet()
    {
        // Cùng dữ liệu đó, nhưng Task Manager nhìn số đã duyệt — không tự nhảy lên 4.
        _tasks.Setup(t => t.SearchAsync(
                TaskType.Recording, null, null, null, null, It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<SortSpec<TaskSortField>>(),
                It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([Row(done: 0)], 1));

        var page = await _service.SearchAsync(
            new TaskSearchRequest { TaskType = TaskType.Recording, Page = 1, PageSize = 20 },
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(0, Assert.Single(page.Items).Progress.Done);
    }
}
