using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.WorkTasks;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// TEAM_018 — cầu nối "nộp bản ghi → hàng chờ task duyệt" chạy trên PostgreSQL thật.
///
/// Đường NỘP qua HTTP nằm ở <c>RecordingAutoQueueApiTests</c>: RecordingService mở transaction
/// riêng khi lưu bản ghi, không lồng được vào transaction bao của IntegrationTestBase. Ở đây gọi
/// thẳng <see cref="ITaskProgressTracker"/> — đúng hàm mà RecordingService gọi — để ghim các luật
/// lọc bằng SQL thật:
///   • vào MỌI task duyệt chưa huỷ của đợt, kể cả task đã đủ chỉ tiêu (TEAM_016)
///   • không vào task duyệt của đợt khác, không vào task đã huỷ
///   • bản trượt QC không tới vòng duyệt nên không vào hàng chờ
///   • bản thu ngoài đợt vẫn để Reviewer nhặt ở hàng chờ chung
/// </summary>
[Trait("Category", "Integration")]
[Collection("Database")]
public sealed class RecordingAutoQueueTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private ITaskProgressTracker Tracker => Scope.ServiceProvider.GetRequiredService<ITaskProgressTracker>();

    /// <summary>Task Manager + đợt đã giao cho họ — mọi task trong test đều qua đường này.</summary>
    private async Task<(AppUser Manager, Campaign Campaign)> TaoDotAsync(string nhan, CancellationToken ct)
    {
        var manager = await CreateUserAsync($"tm-{nhan}-{Guid.NewGuid():N}@test.local", RoleName.TaskManager);

        var campaign = await CreateCampaignAsync($"Đợt {nhan} {Guid.NewGuid():N}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), manager.UserId, CampaignStatus.Open);

        campaign.AssignedTo = manager.UserId;
        await Db.SaveChangesAsync(ct);

        return (manager, campaign);
    }

    /// <summary>Task thu âm trong đợt: câu đã duyệt nội dung + lượt giao Active cho Speaker.</summary>
    private async Task<(WorkTask Task, Script Script)> TaoTaskGhiAmAsync(
        Campaign campaign, AppUser manager, AppUser speaker, CancellationToken ct)
    {
        var task = await CreateTaskAsync(campaign.CampaignId, TaskType.Recording, 5,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        var script = await CreateValidatedScriptAsync(
            $"[vi]Em [en]thu{Guid.NewGuid():N} [vi]hàng đợi", "[vi]Em thu hàng đợi",
            ScriptDomain.ItTechnology, speaker.UserId);

        Db.TaskScripts.Add(new TaskScript
        {
            TaskId = task.TaskId,
            ScriptId = script.ScriptId,
            Status = TaskScriptStatus.Pending
        });

        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = task.TaskId,
            UserId = speaker.UserId,
            AssignmentStatus = AssignmentStatus.Active,
            AssignedAt = DateTimeOffset.UtcNow
        });

        await Db.SaveChangesAsync(ct);
        return (task, script);
    }

    private Task<List<TaskRecording>> HangChoCuaBanGhiAsync(string recordingId, CancellationToken ct) =>
        Db.TaskRecordings.AsNoTracking()
            .Where(tr => tr.RecordingId == recordingId)
            .OrderBy(tr => tr.TaskId)
            .ToListAsync(ct);

    [Fact]
    public async Task BanChoTuyetCuaDot_VaoMoiTaskDuyetChuaHuy()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, campaign) = await TaoDotAsync("auto-queue", ct);
        var (managerDotKhac, dotKhac) = await TaoDotAsync("dot-khac", ct);
        var speaker = await CreateUserAsync($"sp-aq-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var reviewer = await CreateUserAsync($"rv-aq-{Guid.NewGuid():N}@test.local", RoleName.Reviewer);

        var (recTask, script) = await TaoTaskGhiAmAsync(campaign, manager, speaker, ct);

        // Ba task duyệt cùng đợt: đang chạy (đã giao Reviewer), đã đủ chỉ tiêu, đã huỷ.
        var reviewTask = await CreateTaskAsync(campaign.CampaignId, TaskType.Review, 4,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        var reviewTaskDuChiTieu = await CreateTaskAsync(campaign.CampaignId, TaskType.Review, 1,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));
        reviewTaskDuChiTieu.Status = WorkTaskStatus.Completed;

        var reviewTaskDaHuy = await CreateTaskAsync(campaign.CampaignId, TaskType.Review, 4,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10), WorkTaskStatus.Cancelled);

        // Task duyệt của đợt khác — không được nhận bản ghi này.
        var reviewTaskDotKhac = await CreateTaskAsync(dotKhac.CampaignId, TaskType.Review, 4,
            managerDotKhac.UserId, DateTimeOffset.UtcNow.AddDays(10));

        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = reviewTask.TaskId,
            UserId = reviewer.UserId,
            AssignmentStatus = AssignmentStatus.Active,
            AssignedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(ct);

        // Bản chờ duyệt nằm trong task thu âm của đợt — đúng trạng thái RecordingService lưu.
        var recording = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        recording.TaskId = recTask.TaskId;
        await Db.SaveChangesAsync(ct);

        await Tracker.OnRecordingSubmittedAsync(recording, ct);

        var hangCho = await HangChoCuaBanGhiAsync(recording.RecordingId, ct);

        // Đang chạy lẫn đã đủ chỉ tiêu đều nhận (TEAM_016: KPI không khoá việc).
        Assert.Equal(2, hangCho.Count);
        Assert.All(hangCho, tr =>
        {
            Assert.Equal(TaskRecordingStatus.Queued, tr.Status);
            Assert.NotEqual(default(DateTimeOffset), tr.IncludedAt);
        });
        Assert.Contains(hangCho, tr => tr.TaskId == reviewTask.TaskId);
        Assert.Contains(hangCho, tr => tr.TaskId == reviewTaskDuChiTieu.TaskId);

        // Task đã huỷ và task của đợt khác thì không.
        Assert.DoesNotContain(hangCho, tr => tr.TaskId == reviewTaskDaHuy.TaskId);
        Assert.DoesNotContain(hangCho, tr => tr.TaskId == reviewTaskDotKhac.TaskId);
    }

    [Fact]
    public async Task BanTrungQc_KhongVaoHangChoTaskDuyet()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, campaign) = await TaoDotAsync("qc-truot", ct);
        var speaker = await CreateUserAsync($"sp-qc-{Guid.NewGuid():N}@test.local", RoleName.Speaker);

        var (recTask, script) = await TaoTaskGhiAmAsync(campaign, manager, speaker, ct);

        await CreateTaskAsync(campaign.CampaignId, TaskType.Review, 4,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        // Mock QC trong test luôn cho qua, nên dựng thẳng bản trượt rồi gọi đúng cầu nối
        // mà RecordingService dùng — điều được kiểm là chốt trạng thái trong tracker.
        var recording = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        recording.Status = RecordingStatus.QcFailed;
        recording.TaskId = recTask.TaskId;
        await Db.SaveChangesAsync(ct);

        await Tracker.OnRecordingSubmittedAsync(recording, ct);

        Assert.Empty(await HangChoCuaBanGhiAsync(recording.RecordingId, ct));
    }

    [Fact]
    public async Task ThuNgoaiDot_KhongVaoHangChoTaskDuyet()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, campaign) = await TaoDotAsync("ngoai-dot", ct);
        var speaker = await CreateUserAsync($"sp-nd-{Guid.NewGuid():N}@test.local", RoleName.Speaker);

        // Task thu âm độc lập (C1): không thuộc đợt nào.
        var taskTran = new WorkTask
        {
            CampaignId = null,
            TaskType = TaskType.Recording,
            TargetQty = 5,
            Deadline = DateTimeOffset.UtcNow.AddDays(10),
            Status = WorkTaskStatus.InProgress,
            CreatedBy = manager.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        Db.WorkTasks.Add(taskTran);
        await Db.SaveChangesAsync(ct);

        var script = await CreateValidatedScriptAsync(
            $"[vi]Em [en]ngoai{Guid.NewGuid():N} [vi]đợt", "[vi]Em ngoài đợt",
            ScriptDomain.ItTechnology, speaker.UserId);

        Db.TaskScripts.Add(new TaskScript
        {
            TaskId = taskTran.TaskId,
            ScriptId = script.ScriptId,
            Status = TaskScriptStatus.Pending
        });
        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = taskTran.TaskId,
            UserId = speaker.UserId,
            AssignmentStatus = AssignmentStatus.Active,
            AssignedAt = DateTimeOffset.UtcNow
        });

        // Có task duyệt đang mở trong đợt — nhưng bản thu này không thuộc đợt nên không được nhận.
        await CreateTaskAsync(campaign.CampaignId, TaskType.Review, 4,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));
        await Db.SaveChangesAsync(ct);

        var recording = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        recording.TaskId = taskTran.TaskId;
        await Db.SaveChangesAsync(ct);

        await Tracker.OnRecordingSubmittedAsync(recording, ct);

        Assert.Empty(await HangChoCuaBanGhiAsync(recording.RecordingId, ct));
    }
}
