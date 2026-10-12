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
            Mock.Of<ITaskProgressTracker>(),
            Mock.Of<ISystemConfigService>(),
            TimeProvider.System);
    }

    /// <summary>Speaker vừa gửi đủ hai bản, chưa có bản nào được duyệt.</summary>
    private static TaskRow Row(int done = 0, int submittedPairs = 4, int target = 10) => new(
        7, 1, "Chiến dịch", TaskType.Recording, "Thu âm", WorkTaskStatus.InProgress, target,
        null, DateTimeOffset.UtcNow, 9, "Người đọc",
        Started: 8, Done: done, SubmittedPairs: submittedPairs, UsableItems: 10, TotalItems: 10);

    [Fact]
    public async Task Speaker_TienDo_DemCapCauDaThuDu_ChuaCanChoDuyet()
    {
        _tasks.Setup(t => t.GetActiveRowsForAssigneeAsync(9, TaskType.Recording, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Row()]);
        _tasks.Setup(t => t.GetSpeakerTotalsAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((8, 0));

        var result = await _service.GetSpeakerProgressAsync(9, TestContext.Current.CancellationToken);

        var progress = Assert.Single(result.ActiveTasks).Progress;
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

        var result = await _service.GetSpeakerProgressAsync(9, TestContext.Current.CancellationToken);

        var progress = Assert.Single(result.ActiveTasks).Progress;
        Assert.Equal(100, progress.Percent);
    }

    [Fact]
    public async Task TaskManager_DanhSach_VanDemCapCauDaDuyet()
    {
        // Cùng dữ liệu đó, nhưng Task Manager nhìn số đã duyệt — không tự nhảy lên 4.
        _tasks.Setup(t => t.SearchAsync(
                TaskType.Recording, null, null, null, It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([Row(done: 0)], 1));

        var page = await _service.SearchAsync(
            new TaskSearchRequest { TaskType = TaskType.Recording, Page = 1, PageSize = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal(0, Assert.Single(page.Items).Progress.Done);
    }
}
