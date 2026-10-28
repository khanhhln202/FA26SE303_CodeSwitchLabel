using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeSwitchLabel.Tests.Services;

[Trait("Category", "Unit")]
public sealed class TaskServiceAutoFillTests
{
    [Fact]
    public async Task ReviewAutoFillContinuesAfterCandidateIsNoLongerPending()
    {
        var tasks = new Mock<ITaskRepository>(MockBehavior.Strict);
        var tracker = new Mock<ITaskProgressTracker>();
        var config = new Mock<ISystemConfigService>();
        config.Setup(c => c.GetIntAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var service = new TaskService(
            tasks.Object,
            Mock.Of<ICampaignRepository>(),
            Mock.Of<IScriptQueueRepository>(),
            tracker.Object,
            config.Object,
            TimeProvider.System);
        var task = new WorkTask { TaskId = 7, TaskType = TaskType.Review, Status = WorkTaskStatus.InProgress };
        var stale = new Recording { RecordingId = "r_stale", Status = RecordingStatus.Approved };
        var available = new Recording { RecordingId = "r_available", Status = RecordingStatus.PendingReview };

        tasks.Setup(t => t.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDbContextTransaction>());
        tasks.Setup(t => t.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        tasks.Setup(t => t.GetOpenAssignmentsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TaskAssignment>());
        tasks.SetupSequence(t => t.FindRecordingCandidatesAsync(
                7, It.IsAny<long[]>(), null, 1, 3, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["r_stale"])
            .ReturnsAsync(["r_available"]);
        tasks.Setup(t => t.GetRecordingItemsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TaskRecording>());
        tasks.Setup(t => t.GetRecordingsWithReviewsAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .Returns((string[] ids, CancellationToken _) => Task.FromResult(ids
                .Select(id => id == stale.RecordingId ? stale : available)
                .ToList()));
        tasks.Setup(t => t.FindRecordingsQueuedInOtherActiveTasksAsync(
                It.IsAny<string[]>(), 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        tasks.Setup(t => t.AddRecordingItems(It.IsAny<IEnumerable<TaskRecording>>()));
        tasks.Setup(t => t.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        tasks.Setup(t => t.GetRowAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new TaskRow(
            7, null, null, TaskType.Review, ScriptDomain.ItTechnology, null, WorkTaskStatus.InProgress, 1,
            null, DateTimeOffset.UtcNow, [], 0, 0, 0, 0, 0));
        tasks.Setup(t => t.GetAssignmentHistoryAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TaskAssignment>());
        tracker.Setup(t => t.RefreshStatusAsync(7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await service.AddItemsAsync(
            7,
            new AddTaskItemsRequest { AutoFill = new AutoFillCriteria { Count = 1 } },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Added);
        Assert.Contains(result.Skipped, item => item.Id == "r_stale");
        tasks.Verify(t => t.FindRecordingCandidatesAsync(
            7, It.IsAny<long[]>(), null, 1, 3, It.Is<string[]>(ids => ids.Contains("r_stale")),
            It.IsAny<CancellationToken>()), Times.Once);
        tasks.Verify(t => t.AddRecordingItems(It.Is<IEnumerable<TaskRecording>>(items =>
            items.Single().RecordingId == "r_available")), Times.Once);
    }
}
