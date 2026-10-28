using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// xoá cứng task chỉ chặn khi còn dữ liệu nộp THẬT (bản ghi nộp về /
/// lượt duyệt trỏ tới task). Lượt giao và hàng chờ là sổ sách — xoá theo task;
/// trước đây chúng cũng chặn nên task gỡ khỏi đợt xong vẫn không xoá được.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TaskServiceDeleteTests
{
    private readonly Mock<ITaskRepository> _tasks = new(MockBehavior.Strict);
    private readonly TaskService _service;

    public TaskServiceDeleteTests()
    {
        _service = new TaskService(
            _tasks.Object,
            Mock.Of<ICampaignRepository>(),
            Mock.Of<IScriptQueueRepository>(),
            Mock.Of<ITaskProgressTracker>(),
            Mock.Of<ISystemConfigService>(),
            TimeProvider.System);
    }

    private void SetupStandaloneTask(WorkTask task)
    {
        _tasks.Setup(t => t.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDbContextTransaction>());
        _tasks.Setup(t => t.GetForUpdateAsync(task.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
    }

    [Fact]
    public async Task TaskDaGoKhoiDot_ChiConLuotGiaoVaHangCho_XoaDuoc()
    {
        var task = new WorkTask { TaskId = 7, Status = WorkTaskStatus.InProgress, CreatedBy = 1 };
        SetupStandaloneTask(task);
        _tasks.Setup(t => t.HasSubmittedWorkAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _tasks.Setup(t => t.RemoveTask(task));
        _tasks.Setup(t => t.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _service.DeleteAsync(7, TestContext.Current.CancellationToken);

        _tasks.Verify(t => t.RemoveTask(task), Times.Once);
    }

    [Fact]
    public async Task TaskDaCoDuLieuNop_Chan409()
    {
        var task = new WorkTask { TaskId = 7, Status = WorkTaskStatus.InProgress, CreatedBy = 1 };
        SetupStandaloneTask(task);
        _tasks.Setup(t => t.HasSubmittedWorkAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _service.DeleteAsync(7, TestContext.Current.CancellationToken));

        Assert.Equal("task_has_work", ex.Code);
        _tasks.Verify(t => t.RemoveTask(It.IsAny<WorkTask>()), Times.Never);
    }
}
