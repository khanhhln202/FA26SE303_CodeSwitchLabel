using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Tests.Integration.Repositories;

/// <summary>Integration tests for TaskRepository.</summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public class TaskRepositoryTests : IntegrationTestBase
{
    public TaskRepositoryTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsTask_WhenTaskExists()
    {
        // Arrange
        var task = await CreateTaskAsync(CampaignId, TaskType.Recording, 50, ManagerUserId);

        // Act
        var result = await Tasks.GetForUpdateAsync(task.TaskId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(task.TaskId, result.TaskId);
        Assert.Equal(50, result.TargetQty);
    }

    [Fact]
    public async Task GetRowAsync_ReturnsTaskRow_WhenTaskExists()
    {
        // Arrange
        var task = await CreateTaskAsync(CampaignId, TaskType.Recording, 50, ManagerUserId);

        // Act
        var result = await Tasks.GetRowAsync(task.TaskId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(task.TaskId, result.TaskId);
        Assert.Equal(50, result.TargetQty);
        Assert.Equal(0, result.Done);
    }

    [Fact]
    public async Task SearchAsync_ReturnsTasks_WhenTasksExist()
    {
        // Arrange
        await CreateTaskAsync(CampaignId, TaskType.Recording, 50, ManagerUserId);
        await CreateTaskAsync(CampaignId, TaskType.Review, 30, ManagerUserId);

        // Act
        var (items, total) = await Tasks.SearchAsync(null, null, null, null, DateTimeOffset.UtcNow, 1, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(total >= 2);
    }

    [Fact]
    public async Task GetActiveRowsForAssigneeAsync_ReturnsEmpty_WhenNoAssignments()
    {
        // Act
        var items = await Tasks.GetActiveRowsForAssigneeAsync(ManagerUserId, TaskType.Recording, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task IsActiveTaskOfAsync_ReturnsFalse_WhenNotAssigned()
    {
        // Arrange
        var task = await CreateTaskAsync(CampaignId, TaskType.Recording, 50, ManagerUserId);

        // Act
        var result = await Tasks.IsActiveTaskOfAsync(task.TaskId, ReviewerUserId, TaskType.Recording, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetAssignableUsersAsync_RanksByApprovalThenOverdueThenLoad()
    {
        // TEAM_002: speaker prioritization — approval DESC, overdue ASC, load ASC.
        var good = await CreateUserWithBuilderAsync(RoleName.Speaker, NewUniqueEmail("rank-good"), "Rank Good");
        var bad = await CreateUserWithBuilderAsync(RoleName.Speaker, NewUniqueEmail("rank-bad"), "Rank Bad");
        var fresh = await CreateUserWithBuilderAsync(RoleName.Speaker, NewUniqueEmail("rank-fresh"), "Rank Fresh");

        // Two scripts: one speaker each — the single-speaker trigger forbids
        // two speakers recording the same pair.
        var goodScript = await CreateValidatedScriptAsync(
            "[vi]Xếp [en]rank [vi]ưu tiên", "[vi]Xếp hạng ưu tiên",
            ScriptDomain.ItTechnology, AdminUserId);
        var badScript = await CreateValidatedScriptAsync(
            "[vi]Xếp [en]rank [vi]thấp", "[vi]Xếp hạng thấp",
            ScriptDomain.ItTechnology, AdminUserId);

        // Good: 1 approved. Bad: 1 rejected (0% approval, sorts after good).
        var goodRec = await CreateRecordingAsync(goodScript.ScriptId, good.UserId, SentenceVariant.CodeSwitching);
        goodRec.Status = RecordingStatus.Approved;
        var badRec = await CreateRecordingAsync(badScript.ScriptId, bad.UserId, SentenceVariant.CodeSwitching);
        badRec.Status = RecordingStatus.Rejected;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Overdue active task on good speaker — approval still wins, but overdue is
        // visible for Task Manager triage. The deadline trigger requires the date
        // inside the campaign window, so pull the campaign start back first
        // (per-test transaction rolls this back).
        var campaign = await Db.Campaigns.FirstAsync(c => c.CampaignId == CampaignId, TestContext.Current.CancellationToken);
        campaign.StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-10));
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var overdue = await CreateTaskAsync(CampaignId, TaskType.Recording, 1, ManagerUserId,
            deadline: DateTimeOffset.UtcNow.AddDays(-1), status: WorkTaskStatus.Open);
        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = overdue.TaskId,
            UserId = good.UserId,
            AssignedAt = DateTimeOffset.UtcNow,
            AssignmentStatus = AssignmentStatus.Active
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var rows = await Tasks.GetAssignableUsersAsync(RoleName.Speaker, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        var ids = rows.Where(r => r.UserId == good.UserId || r.UserId == bad.UserId || r.UserId == fresh.UserId)
            .Select(r => r.UserId).ToList();

        // Assert — good (100%) before bad (0%) before fresh (no recordings = NULL).
        Assert.Equal([good.UserId, bad.UserId, fresh.UserId], ids);
        Assert.Equal(1, rows.First(r => r.UserId == good.UserId).OverdueTasks);
        Assert.Equal(100m, rows.First(r => r.UserId == good.UserId).ApprovalRatePct);
    }
}
