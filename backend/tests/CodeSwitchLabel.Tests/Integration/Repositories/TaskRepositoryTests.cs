using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
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
        var (items, total) = await Tasks.SearchAsync(null, null, null, null, null, DateTimeOffset.UtcNow, 1, 10, default, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(total >= 2);
    }

    // ---------------------------------------------------------------- TEAM_012: sắp xếp

    /// <summary>
    /// Ba task với chỉ tiêu khác nhau; chỉ một task có hạn. Danh sách task không lọc được theo
    /// tên nên test lấy cả trang rồi chỉ giữ lại ba cái vừa tạo để so thứ tự tương đối.
    /// </summary>
    private async Task<WorkTask[]> CreateThreeSortedTasksAsync()
    {
        // Trigger trg_task_campaign_window: hạn của task phải nằm trong cửa sổ chiến dịch,
        // tính theo giờ Việt Nam — nên lấy hạn từ chính chiến dịch đó, không đặt mốc cứng.
        // Npgsql chỉ nhận DateTimeOffset có offset 0, nên phải quy ra UTC.
        var campaign = await Campaigns.GetAsync(CampaignId, TestContext.Current.CancellationToken);
        var deadline = new DateTimeOffset(
                campaign!.StartDate.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7))
            .ToUniversalTime();

        return
        [
            await CreateTaskAsync(CampaignId, TaskType.Recording, 30, ManagerUserId, deadline),
            await CreateTaskAsync(CampaignId, TaskType.Recording, 10, ManagerUserId),
            await CreateTaskAsync(CampaignId, TaskType.Recording, 20, ManagerUserId)
        ];
    }

    private static List<long> RelativeOrder(IEnumerable<TaskRow> page, IReadOnlyCollection<WorkTask> mine)
    {
        var ids = mine.Select(t => t.TaskId).ToHashSet();
        return [.. page.Where(t => ids.Contains(t.TaskId)).Select(t => t.TaskId)];
    }

    [Fact]
    public async Task SearchAsync_KhongTruyenSort_GiuNguyenThuTuCoHanTruocVaKhongHanOCuoi()
    {
        // Regression TEAM_012: mặc định của /api/tasks là task có hạn trước, task không hạn ở cuối.
        var mine = await CreateThreeSortedTasksAsync();

        var (items, _) = await Tasks.SearchAsync(
            null, null, null, null, null, DateTimeOffset.UtcNow, 1, 50, default,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [mine[0].TaskId, mine[1].TaskId, mine[2].TaskId],
            RelativeOrder(items, mine));
    }

    [Fact]
    public async Task SearchAsync_SapXepTheoTargetQty_TangDan()
    {
        var mine = await CreateThreeSortedTasksAsync();

        var (items, _) = await Tasks.SearchAsync(
            null, null, null, null, null, DateTimeOffset.UtcNow, 1, 50,
            SortSpec<TaskSortField>.From("targetQty", "asc"), TestContext.Current.CancellationToken);

        // Chỉ tiêu 10, 20, 30 — không phụ thuộc hạn.
        Assert.Equal(
            [mine[1].TaskId, mine[2].TaskId, mine[0].TaskId],
            RelativeOrder(items, mine));
    }

    [Fact]
    public async Task SearchAsync_SapXepTheoDeadline_GiamDanVanGiuTaskKhongHanOCuoi()
    {
        var mine = await CreateThreeSortedTasksAsync();

        var (items, _) = await Tasks.SearchAsync(
            null, null, null, null, null, DateTimeOffset.UtcNow, 1, 50,
            SortSpec<TaskSortField>.From("deadline", "desc"), TestContext.Current.CancellationToken);

        // Postgres mặc định NULLS FIRST khi DESC — task không hạn phải ở cuối, không phải đầu.
        Assert.Equal(
            [mine[0].TaskId, mine[1].TaskId, mine[2].TaskId],
            RelativeOrder(items, mine));
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
