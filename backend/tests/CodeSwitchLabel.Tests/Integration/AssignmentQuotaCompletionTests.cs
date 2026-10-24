using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.WorkTasks;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// V17 — lượt giao chuyển Completed khi người nhận đạt quota riêng.
///
/// Luật đang ghim ở đây:
///   • done >= quota → Completed; done < quota → Active
///   • Completed KHÔNG phải trạng thái cuối và không khoá công việc (TEAM_016):
///     người nhận vẫn mở được task và nhận thêm việc
///   • nâng chỉ tiêu làm quota tăng thì lượt giao tự mở lại Active
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class AssignmentQuotaCompletionTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private ITaskProgressTracker Tracker =>
        Scope.ServiceProvider.GetRequiredService<ITaskProgressTracker>();

    [Fact]
    public async Task GhiAm_DatQuotaRieng_ChuyenCompleted_VaVanNhanViec()
    {
        var ct = TestContext.Current.CancellationToken;
        var (task, assignment) = await TaoGhiAmAsync(quota: 1, done: 1, ct);

        await Tracker.RefreshStatusAsync(task.TaskId, ct);

        Assert.Equal(AssignmentStatus.Completed, assignment.AssignmentStatus);

        var saved = await Db.WorkTasks.AsNoTracking()
            .SingleAsync(t => t.TaskId == task.TaskId, ct);
        Assert.Equal(WorkTaskStatus.Completed, saved.Status);

        // Completed chỉ nói "đã đạt phần của mình", không phải bị gỡ khỏi task.
        Assert.True(await Tasks.IsActiveTaskOfAsync(
            task.TaskId, SpeakerUserId, TaskType.Recording, ct));
    }

    [Fact]
    public async Task GhiAm_ChuaDatQuota_GiuActive()
    {
        var ct = TestContext.Current.CancellationToken;
        var (task, assignment) = await TaoGhiAmAsync(quota: 2, done: 1, ct);

        await Tracker.RefreshStatusAsync(task.TaskId, ct);

        Assert.Equal(AssignmentStatus.Active, assignment.AssignmentStatus);
        Assert.Equal(WorkTaskStatus.InProgress, (await Db.WorkTasks.AsNoTracking()
            .SingleAsync(t => t.TaskId == task.TaskId, ct)).Status);
    }

    [Fact]
    public async Task GhiAm_NangChiTieu_LuotGiaoMoLaiActive()
    {
        var ct = TestContext.Current.CancellationToken;
        var (task, assignment) = await TaoGhiAmAsync(quota: 1, done: 1, ct);

        await Tracker.RefreshStatusAsync(task.TaskId, ct);
        Assert.Equal(AssignmentStatus.Completed, assignment.AssignmentStatus);

        // Nâng chỉ tiêu lên 2 cần thêm một cặp còn làm được; nếu không UpdateAsync
        // chặn đúng theo target_exceeds_items.
        var next = await CreateValidatedScriptAsync(
            $"[vi]Em [en]thêm {Guid.NewGuid():N} [vi]một câu.",
            $"[vi]Em thêm {Guid.NewGuid():N} một câu.",
            ScriptDomain.ItTechnology, AdminUserId);
        Db.TaskScripts.Add(new TaskScript
        {
            TaskId = task.TaskId,
            ScriptId = next.ScriptId,
            Status = TaskScriptStatus.Pending,
            IncludedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(ct);

        var detail = await TaskService.UpdateAsync(
            task.TaskId, new UpdateTaskRequest { TargetQty = 2 }, ct, ManagerUserId);

        Assert.Equal(WorkTaskStatus.InProgress, detail.Summary.Status);
        var mine = Assert.Single(detail.Assignments, a => a.UserId == SpeakerUserId);
        Assert.Equal(AssignmentStatus.Active, mine.Status);
        Assert.Equal(2, mine.TargetQty);
        Assert.Contains(detail.Summary.Assignees, a => a.UserId == SpeakerUserId);
    }

    [Fact]
    public async Task Duyet_DatQuotaRieng_ChuyenCompleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var task = await CreateTaskAsync(CampaignId, TaskType.Review, 1, ManagerUserId);
        var assignment = new TaskAssignment
        {
            TaskId = task.TaskId,
            UserId = ReviewerUserId,
            TargetQty = 1,
            AssignmentStatus = AssignmentStatus.Active,
            AssignedAt = DateTimeOffset.UtcNow
        };
        Db.TaskAssignments.Add(assignment);

        var script = await CreateValidatedScriptAsync(
            $"[vi]Em [en]duyệt {Guid.NewGuid():N} [vi]câu này.",
            $"[vi]Em duyệt {Guid.NewGuid():N} câu này.",
            ScriptDomain.ItTechnology, AdminUserId);
        var recording = await CreateRecordingAsync(
            script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);
        Db.TaskRecordings.Add(new TaskRecording
        {
            TaskId = task.TaskId,
            RecordingId = recording.RecordingId,
            Status = TaskRecordingStatus.Queued,
            IncludedAt = DateTimeOffset.UtcNow
        });
        Db.Reviews.Add(new Review
        {
            RecordingId = recording.RecordingId,
            ReviewerId = ReviewerUserId,
            TaskId = task.TaskId,
            ReviewRound = 1,
            Decision = ReviewDecision.Approved,
            IsBlind = true,
            ReviewedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(ct);

        await Tracker.RefreshStatusAsync(task.TaskId, ct);

        Assert.Equal(AssignmentStatus.Completed, assignment.AssignmentStatus);
        Assert.True(await Tasks.IsActiveTaskOfAsync(
            task.TaskId, ReviewerUserId, TaskType.Review, ct));
    }

    /// <summary>
    /// Dựng task thu âm + một người nhận có quota riêng, kèm <paramref name="done"/>
    /// cặp câu đã duyệt đạt. Không đi qua API để test tập trung vào đúng bước chấm
    /// trạng thái của TaskProgressTracker.
    /// </summary>
    private async Task<(WorkTask Task, TaskAssignment Assignment)> TaoGhiAmAsync(
        int quota, int done, CancellationToken ct)
    {
        var task = await CreateTaskAsync(CampaignId, TaskType.Recording, quota, ManagerUserId);
        var assignment = new TaskAssignment
        {
            TaskId = task.TaskId,
            UserId = SpeakerUserId,
            TargetQty = quota,
            AssignmentStatus = AssignmentStatus.Active,
            AssignedAt = DateTimeOffset.UtcNow
        };
        Db.TaskAssignments.Add(assignment);

        for (var i = 0; i < done; i++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var script = await CreateValidatedScriptAsync(
                $"[vi]Em [en]test {suffix} [vi]câu {i}.",
                $"[vi]Em câu {suffix} {i}.",
                ScriptDomain.ItTechnology, AdminUserId);

            var cs = await CreateRecordingAsync(
                script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);
            cs.Status = RecordingStatus.Approved;
            var vi = await CreateRecordingAsync(
                script.ScriptId, SpeakerUserId, SentenceVariant.PureVietnamese);
            vi.Status = RecordingStatus.Approved;

            Db.TaskScripts.Add(new TaskScript
            {
                TaskId = task.TaskId,
                ScriptId = script.ScriptId,
                Status = TaskScriptStatus.Completed,
                IncludedAt = DateTimeOffset.UtcNow
            });
            await Db.SaveChangesAsync(ct);
        }

        return (task, assignment);
    }
}
