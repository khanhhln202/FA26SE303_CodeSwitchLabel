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
        var task = await CreateTaskAsync(WaveId, TaskType.Recording, 50, ManagerUserId);

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
        var task = await CreateTaskAsync(WaveId, TaskType.Recording, 50, ManagerUserId);

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
        await CreateTaskAsync(WaveId, TaskType.Recording, 50, ManagerUserId);
        await CreateTaskAsync(WaveId, TaskType.Review, 30, ManagerUserId);

        // Act
        var (items, total) = await Tasks.SearchAsync(null, null, null, null, null, DateTimeOffset.UtcNow, 1, 10, default, null, false, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(total >= 2);
    }

    // ---------------------------------------------------------------- sắp xếp

    /// <summary>
    /// Ba task với chỉ tiêu khác nhau; chỉ một task có hạn. Danh sách task không lọc được theo
    /// tên nên test lấy cả trang rồi chỉ giữ lại ba cái vừa tạo để so thứ tự tương đối.
    /// </summary>
    private async Task<WorkTask[]> CreateThreeSortedTasksAsync()
    {
        // Trigger trg_task_wave_window: hạn của task phải nằm trong cửa sổ chiến dịch,
        // tính theo giờ Việt Nam — nên lấy hạn từ chính chiến dịch đó, không đặt mốc cứng.
        // Npgsql chỉ nhận DateTimeOffset có offset 0, nên phải quy ra UTC.
        var wave = await Waves.GetAsync(WaveId, TestContext.Current.CancellationToken);
        var deadline = new DateTimeOffset(
                wave!.StartDate.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7))
            .ToUniversalTime();

        return
        [
            await CreateTaskAsync(WaveId, TaskType.Recording, 30, ManagerUserId, deadline),
            await CreateTaskAsync(WaveId, TaskType.Recording, 10, ManagerUserId),
            await CreateTaskAsync(WaveId, TaskType.Recording, 20, ManagerUserId)
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
        // Regression mặc định của /api/tasks là task có hạn trước, task không hạn ở cuối.
        var mine = await CreateThreeSortedTasksAsync();

        var (items, _) = await Tasks.SearchAsync(
            null, null, null, null, null, DateTimeOffset.UtcNow, 1, 50, default,
            null, false, TestContext.Current.CancellationToken);

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
            SortSpec<TaskSortField>.From("targetQty", "asc"),
            null, false, TestContext.Current.CancellationToken);

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
            SortSpec<TaskSortField>.From("deadline", "desc"),
            null, false, TestContext.Current.CancellationToken);

        // Postgres mặc định NULLS FIRST khi DESC — task không hạn phải ở cuối, không phải đầu.
        Assert.Equal(
            [mine[0].TaskId, mine[1].TaskId, mine[2].TaskId],
            RelativeOrder(items, mine));
    }

    [Fact]
    public async Task GetAssigneeDoneAsync_CapCuaNguoiKhacBiTuChoi_TinhChoNguoiDaDuyet()
    {
        var ct = TestContext.Current.CancellationToken;
        var task = await CreateTaskAsync(WaveId, TaskType.Recording, 1, ManagerUserId);
        var rejectedSpeaker = await CreateUserWithBuilderAsync(
            RoleName.Speaker, NewUniqueEmail("done-rejected"), "Done Rejected");
        var approvedSpeaker = await CreateUserWithBuilderAsync(
            RoleName.Speaker, NewUniqueEmail("done-approved"), "Done Approved");

        var script = await CreateValidatedScriptAsync(
            "[vi]Câu [en]done [vi]rồi", "[vi]Câu done rồi",
            ScriptDomain.ItTechnology, approvedSpeaker.UserId);
        Db.TaskScripts.Add(new TaskScript
        {
            TaskId = task.TaskId, ScriptId = script.ScriptId, Status = TaskScriptStatus.Completed
        });

        // A thu bản đầu rồi bị loại; B thu lại và đạt cả hai biến thể.
        var rejected = await CreateRecordingAsync(script.ScriptId, rejectedSpeaker.UserId, SentenceVariant.CodeSwitching);
        rejected.Status = RecordingStatus.Rejected;
        await Db.SaveChangesAsync(ct);

        var cs = await CreateRecordingAsync(script.ScriptId, approvedSpeaker.UserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, approvedSpeaker.UserId, SentenceVariant.PureVietnamese);
        cs.Status = RecordingStatus.Approved;
        vi.Status = RecordingStatus.Approved;
        await Db.SaveChangesAsync(ct);

        var done = await Tasks.GetAssigneeDoneAsync(task.TaskId, TaskType.Recording, ct);

        Assert.False(done.ContainsKey(rejectedSpeaker.UserId));
        Assert.Equal(1, done[approvedSpeaker.UserId]);
    }

    [Fact]
    public async Task FindScriptCandidatesAsync_BanBiTuChoi_KhongGiuCho()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await CreateUserWithBuilderAsync(RoleName.Speaker, NewUniqueEmail("cand-owner"), "Cand Owner");
        var other = await CreateUserWithBuilderAsync(RoleName.Speaker, NewUniqueEmail("cand-other"), "Cand Other");

        var script = await CreateValidatedScriptAsync(
            "[vi]Câu [en]candidate [vi]này", "[vi]Câu candidate này",
            ScriptDomain.ItTechnology, owner.UserId);

        var rejected = await CreateRecordingAsync(script.ScriptId, owner.UserId, SentenceVariant.CodeSwitching);
        rejected.Status = RecordingStatus.Rejected;
        await Db.SaveChangesAsync(ct);

        var assignedTask = await CreateTaskAsync(WaveId, TaskType.Recording, 1, ManagerUserId);
        var openTask = await CreateTaskAsync(WaveId, TaskType.Recording, 1, ManagerUserId);

        // Người khác nhận task vẫn thấy câu vì bản rejected đã trả cặp về hàng đợi.
        var forOther = await Tasks.FindScriptCandidatesAsync(
            assignedTask.TaskId, [other.UserId], ScriptDomain.ItTechnology, 100, [], ct);
        Assert.Contains(script.ScriptId, forOther);

        // Task chưa giao cho ai cũng vậy.
        var unassigned = await Tasks.FindScriptCandidatesAsync(
            openTask.TaskId, [], ScriptDomain.ItTechnology, 100, [], ct);
        Assert.Contains(script.ScriptId, unassigned);
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
        var task = await CreateTaskAsync(WaveId, TaskType.Recording, 50, ManagerUserId);

        // Act
        var result = await Tasks.IsActiveTaskOfAsync(task.TaskId, ReviewerUserId, TaskType.Recording, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetRecordableScriptIdsByDomainAsync_TachTheoTungSpeaker()
    {
        // free: chưa ai đụng → nhận được
        var free = await CreateValidatedScriptAsync(
            "[vi]Câu [en]free", "[vi]Câu tự do", ScriptDomain.ItTechnology, AdminUserId);
        // otherBusy: Speaker KHÁC đang có bản chờ duyệt → vẫn nhận được (dùng chung câu)
        var otherBusy = await CreateValidatedScriptAsync(
            "[vi]Câu [en]other", "[vi]Câu người khác", ScriptDomain.ItTechnology, AdminUserId);
        // mineBusy: chính Speaker này đã có bản → không giao lại
        var mineBusy = await CreateValidatedScriptAsync(
            "[vi]Câu [en]mine", "[vi]Câu của tôi", ScriptDomain.ItTechnology, AdminUserId);
        // mineQueued: đang là việc Pending của Speaker trong task còn chạy → không giao trùng
        var mineQueued = await CreateValidatedScriptAsync(
            "[vi]Câu [en]queued", "[vi]Câu đang làm", ScriptDomain.ItTechnology, AdminUserId);
        // mineCancelled: việc cũ đã huỷ → nhận lại được
        var mineCancelled = await CreateValidatedScriptAsync(
            "[vi]Câu [en]cancelled", "[vi]Câu đã huỷ", ScriptDomain.ItTechnology, AdminUserId);

        await CreateRecordingAsync(otherBusy.ScriptId, ReviewerUserId, SentenceVariant.CodeSwitching);
        await CreateRecordingAsync(mineBusy.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        var running = await CreateTaskAsync(
            WaveId, TaskType.Recording, 1, ManagerUserId, status: WorkTaskStatus.InProgress);
        var cancelled = await CreateTaskAsync(
            WaveId, TaskType.Recording, 1, ManagerUserId, status: WorkTaskStatus.Cancelled);

        Db.TaskScripts.AddRange(
            new TaskScript
            {
                TaskId = running.TaskId,
                ScriptId = mineQueued.ScriptId,
                AssigneeUserId = SpeakerUserId,
                Status = TaskScriptStatus.Pending,
                IncludedAt = DateTimeOffset.UtcNow
            },
            new TaskScript
            {
                TaskId = cancelled.TaskId,
                ScriptId = mineCancelled.ScriptId,
                AssigneeUserId = SpeakerUserId,
                Status = TaskScriptStatus.Pending,
                IncludedAt = DateTimeOffset.UtcNow
            });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var available = await Tasks.GetRecordableScriptIdsByDomainAsync(
            ScriptDomain.ItTechnology, SpeakerUserId, 10, TestContext.Current.CancellationToken);

        Assert.Contains(free.ScriptId, available);
        Assert.Contains(otherBusy.ScriptId, available);
        Assert.Contains(mineCancelled.ScriptId, available);
        Assert.DoesNotContain(mineBusy.ScriptId, available);
        Assert.DoesNotContain(mineQueued.ScriptId, available);
    }

    [Fact]
    public async Task GetAssignableUsersAsync_RanksByApprovalThenOverdueThenLoad()
    {
        // speaker prioritization — approval DESC, overdue ASC, load ASC.
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
        // inside the wave window, so pull the wave start back first
        // (per-test transaction rolls this back).
        var wave = await Db.Waves.FirstAsync(c => c.WaveId == WaveId, TestContext.Current.CancellationToken);
        wave.StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-10));
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var overdue = await CreateTaskAsync(WaveId, TaskType.Recording, 1, ManagerUserId,
            deadline: DateTimeOffset.UtcNow.AddDays(-1), status: WorkTaskStatus.InProgress);
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
