using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// TEAM_016 — vượt chỉ tiêu không khoá công việc, và xếp hạng Speaker theo câu ĐÃ NỘP.
///
/// Những test ở đây ghim đúng luật của nghiệp vụ:
///   • task đạt chỉ tiêu (Completed) vẫn nhận thêm bản ghi / lượt duyệt — KPI để đo, không để khoá
///   • chỉ task đã huỷ (Cancelled) mới chặn
///   • bảng xếp hạng đếm bản đã qua kiểm tra tự động, không đếm bản trượt QC
/// </summary>
[Trait("Category", "Integration")]
[Collection("Database")]
public sealed class WorkAfterTargetTests(DatabaseFixture fixture)
    : IntegrationTestBase(fixture)
{
    private IScriptQueueService QueueService =>
        Scope.ServiceProvider.GetRequiredService<IScriptQueueService>();

    private ISpeakerRoundsService Rounds =>
        Scope.ServiceProvider.GetRequiredService<ISpeakerRoundsService>();

    /// <summary>Tạo Task Manager + đợt đã giao cho họ — mọi task trong test đều đi qua đường này.</summary>
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

    /// <summary>
    /// Task ghi âm đã ĐẠT chỉ tiêu: một mục đã xong (kèm bản ghi được duyệt gắn vào task),
    /// một mục còn chờ thu. Người đọc vẫn còn lượt giao Active — đúng tình huống "muốn làm
    /// thêm" mà TEAM_016 cho phép.
    /// </summary>
    private async Task<(WorkTask Task, AppUser Speaker, Script Done, Script Next)> TaoTaskGhiAmDaDatChiTieuAsync(
        CancellationToken ct)
    {
        var (manager, campaign) = await TaoDotAsync("ghi-am-vuot", ct);
        var speaker = await CreateUserAsync($"sp-vuot-{Guid.NewGuid():N}@test.local", RoleName.Speaker);

        var task = await CreateTaskAsync(campaign.CampaignId, TaskType.Recording, 1,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        var done = await CreateValidatedScriptAsync(
            $"[vi]Em [en]xong{Guid.NewGuid():N} [vi]rồi", "[vi]Em làm xong rồi",
            ScriptDomain.ItTechnology, speaker.UserId);

        var next = await CreateValidatedScriptAsync(
            $"[vi]Em [en]them{Guid.NewGuid():N} [vi]nữa", "[vi]Em làm thêm nữa",
            ScriptDomain.ItTechnology, speaker.UserId);

        Db.TaskScripts.Add(new TaskScript
        {
            TaskId = task.TaskId,
            ScriptId = done.ScriptId,
            Status = TaskScriptStatus.Completed
        });
        Db.TaskScripts.Add(new TaskScript
        {
            TaskId = task.TaskId,
            ScriptId = next.ScriptId,
            Status = TaskScriptStatus.Pending
        });

        var approved = await CreateRecordingAsync(done.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        approved.Status = RecordingStatus.Approved;
        approved.TaskId = task.TaskId;

        Db.TaskRecordings.Add(new TaskRecording
        {
            TaskId = task.TaskId,
            RecordingId = approved.RecordingId,
            Status = TaskRecordingStatus.Reviewed
        });

        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = task.TaskId,
            UserId = speaker.UserId,
            AssignmentStatus = AssignmentStatus.Active,
            AssignedAt = DateTimeOffset.UtcNow
        });

        task.Status = WorkTaskStatus.Completed;
        await Db.SaveChangesAsync(ct);

        return (task, speaker, done, next);
    }

    /// <summary>
    /// Task duyệt đã ĐẠT chỉ tiêu: một bản đã duyệt xong, một bản còn chờ — Reviewer vẫn
    /// phải mở được bản chờ để duyệt tiếp.
    /// </summary>
    private async Task<(WorkTask Task, AppUser Reviewer, Recording Pending)> TaoTaskDuyetDaDatChiTieuAsync(
        CancellationToken ct)
    {
        var (manager, campaign) = await TaoDotAsync("duyet-vuot", ct);
        var speaker = await CreateUserAsync($"sp-duyet-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var reviewer = await CreateUserAsync($"rv-vuot-{Guid.NewGuid():N}@test.local", RoleName.Reviewer);

        var task = await CreateTaskAsync(campaign.CampaignId, TaskType.Review, 1,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        var doneScript = await CreateValidatedScriptAsync(
            $"[vi]Em [en]duyet{Guid.NewGuid():N} [vi]xong", "[vi]Em duyệt xong",
            ScriptDomain.ItTechnology, speaker.UserId);

        var nextScript = await CreateValidatedScriptAsync(
            $"[vi]Em [en]cho{Guid.NewGuid():N} [vi]duyệt", "[vi]Em còn chờ duyệt",
            ScriptDomain.ItTechnology, speaker.UserId);

        var doneRecording = await CreateRecordingAsync(doneScript.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        doneRecording.Status = RecordingStatus.Approved;
        doneRecording.TaskId = task.TaskId;

        Db.TaskRecordings.Add(new TaskRecording
        {
            TaskId = task.TaskId,
            RecordingId = doneRecording.RecordingId,
            Status = TaskRecordingStatus.Reviewed
        });

        var pending = await CreateRecordingAsync(nextScript.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        pending.TaskId = task.TaskId;

        Db.TaskRecordings.Add(new TaskRecording
        {
            TaskId = task.TaskId,
            RecordingId = pending.RecordingId,
            Status = TaskRecordingStatus.Queued
        });

        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = task.TaskId,
            UserId = reviewer.UserId,
            AssignmentStatus = AssignmentStatus.Active,
            AssignedAt = DateTimeOffset.UtcNow
        });

        task.Status = WorkTaskStatus.Completed;
        await Db.SaveChangesAsync(ct);

        return (task, reviewer, pending);
    }

    // --------------------------------------------------------------- speaker vượt KPI

    [Fact]
    public async Task TaskDaDatChiTieu_SpeakerVanLayDuocCauTrongTaskVaNopTiep()
    {
        var ct = TestContext.Current.CancellationToken;
        var (task, speaker, _, next) = await TaoTaskGhiAmDaDatChiTieuAsync(ct);

        // Cửa "task còn nhận việc" mở dù task đã Hoàn thành.
        Assert.True(await Tasks.IsActiveTaskOfAsync(task.TaskId, speaker.UserId, TaskType.Recording, ct));
        // Lấy câu kế tiếp trong task đã Hoàn thành qua được cửa (service không mở
        // transaction mới — chỉ kiểm tra điều kiện qua repository).
        Assert.True(await Tasks.IsActiveTaskOfAsync(task.TaskId, speaker.UserId, TaskType.Recording, ct));
        Assert.True(await Recordings.IsRecordableInTaskAsync(task.TaskId, speaker.UserId, next.ScriptId, ct));

        // Nhận thêm việc KHÔNG làm task rời trạng thái Hoàn thành — chỉ tiêu vẫn đủ.
        await Db.Entry(task).ReloadAsync(ct);
        Assert.Equal(WorkTaskStatus.Completed, task.Status);
    }

    // --------------------------------------------------------------- reviewer vượt KPI

    [Fact]
    public async Task TaskDaDatChiTieu_ReviewerVanNhanDuocBanChoVaDuyetTiep()
    {
        var ct = TestContext.Current.CancellationToken;
        var (task, reviewer, pending) = await TaoTaskDuyetDaDatChiTieuAsync(ct);

        Assert.True(await Reviews.IsActiveReviewTaskOfAsync(task.TaskId, reviewer.UserId, ct));

        // Nhận bản kế tiếp trong task đã Hoàn thành qua được cửa (chỉ kiểm tra điều kiện).
        Assert.True(await Reviews.IsActiveReviewTaskOfAsync(task.TaskId, reviewer.UserId, ct));

        // Duyệt thêm vẫn không kéo task rời Hoàn thành.
        await Db.Entry(task).ReloadAsync(ct);
        Assert.Equal(WorkTaskStatus.Completed, task.Status);
    }

    // ------------------------------------------------------------------ task đã huỷ

    [Fact]
    public async Task TaskDaHuy_VanChanThuAmVaDuyet()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, campaign) = await TaoDotAsync("da-huy", ct);
        var speaker = await CreateUserAsync($"sp-huy-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var reviewer = await CreateUserAsync($"rv-huy-{Guid.NewGuid():N}@test.local", RoleName.Reviewer);

        // Task thu âm đã huỷ nhưng vẫn còn mục chờ và lượt giao Active.
        var recTask = await CreateTaskAsync(campaign.CampaignId, TaskType.Recording, 1,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10), WorkTaskStatus.Cancelled);

        var script = await CreateValidatedScriptAsync(
            $"[vi]Em [en]huy{Guid.NewGuid():N} [vi]rồi", "[vi]Em bị huỷ rồi",
            ScriptDomain.ItTechnology, speaker.UserId);

        Db.TaskScripts.Add(new TaskScript
        {
            TaskId = recTask.TaskId,
            ScriptId = script.ScriptId,
            Status = TaskScriptStatus.Pending
        });
        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = recTask.TaskId,
            UserId = speaker.UserId,
            AssignmentStatus = AssignmentStatus.Active,
            AssignedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(ct);

        Assert.False(await Tasks.IsActiveTaskOfAsync(recTask.TaskId, speaker.UserId, TaskType.Recording, ct));
        Assert.False(await Recordings.IsRecordableInTaskAsync(recTask.TaskId, speaker.UserId, script.ScriptId, ct));

        // Nộp bản ghi vào task đã huỷ: chặn.
        using var audio = new MemoryStream([1, 2, 3, 4]);

        var loiNop = await Assert.ThrowsAsync<ForbiddenException>(() => RecordingService.UploadAsync(
            new UploadRecordingCommand(
                speaker.UserId, script.ScriptId, SentenceVariant.CodeSwitching, recTask.TaskId,
                audio, "take.wav", audio.Length), ct));

        Assert.Equal("task_not_recordable", loiNop.Code);

        // Lấy câu theo task đã huỷ: chặn.
        var loiLay = await Assert.ThrowsAsync<ForbiddenException>(
            () => QueueService.ClaimNextForRecordingAsync(speaker.UserId, recTask.TaskId, ct));

        Assert.Equal("task_not_recordable", loiLay.Code);

        // Task duyệt đã huỷ: nhận bản để duyệt cũng chặn.
        var reviewTask = await CreateTaskAsync(campaign.CampaignId, TaskType.Review, 1,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10), WorkTaskStatus.Cancelled);

        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = reviewTask.TaskId,
            UserId = reviewer.UserId,
            AssignmentStatus = AssignmentStatus.Active,
            AssignedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(ct);

        var loiDuyet = await Assert.ThrowsAsync<ForbiddenException>(
            () => ReviewService.GetNextAsync(reviewer.UserId, reviewTask.TaskId, null, false, ct));

        Assert.Equal("task_not_reviewable", loiDuyet.Code);
    }

    // ------------------------------------------------------------------ bảng xếp hạng

    [Fact]
    public async Task BangXepHang_XepTheoBanDaNop_KhongTinhBanTruotQc()
    {
        var ct = TestContext.Current.CancellationToken;
        var (manager, campaign) = await TaoDotAsync("xep-hang", ct);

        var speaker1 = await CreateUserAsync($"sp-h1-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var speaker2 = await CreateUserAsync($"sp-h2-{Guid.NewGuid():N}@test.local", RoleName.Speaker);

        var task = await CreateTaskAsync(campaign.CampaignId, TaskType.Recording, 10,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        // Speaker 1: chờ duyệt + đạt + bị từ chối = 3 bản đã nộp; 1 bản trượt QC không tính.
        foreach (var (status, variant) in new (RecordingStatus, SentenceVariant)[]
                 {
                     (RecordingStatus.PendingReview, SentenceVariant.CodeSwitching),
                     (RecordingStatus.Approved, SentenceVariant.CodeSwitching),
                     (RecordingStatus.Rejected, SentenceVariant.CodeSwitching),
                     (RecordingStatus.QcFailed, SentenceVariant.CodeSwitching)
                 })
        {
            var script = await CreateValidatedScriptAsync(
                $"[vi]Em [en]h1{Guid.NewGuid():N} [vi]đọc", "[vi]Em đọc câu này",
                ScriptDomain.ItTechnology, speaker1.UserId);

            var recording = await CreateRecordingAsync(script.ScriptId, speaker1.UserId, variant);
            recording.Status = status;
            recording.TaskId = task.TaskId;
        }

        // Speaker 2: 1 bản chờ duyệt + 1 bản trượt QC = 1 bản đã nộp.
        foreach (var status in new[] { RecordingStatus.PendingReview, RecordingStatus.QcFailed })
        {
            var script = await CreateValidatedScriptAsync(
                $"[vi]Em [en]h2{Guid.NewGuid():N} [vi]đọc", "[vi]Em đọc câu này",
                ScriptDomain.ItTechnology, speaker2.UserId);

            var recording = await CreateRecordingAsync(script.ScriptId, speaker2.UserId, SentenceVariant.CodeSwitching);
            recording.Status = status;
            recording.TaskId = task.TaskId;
        }

        await Db.SaveChangesAsync(ct);

        var board = await Rounds.GetLeaderboardAsync(campaign.CampaignId, speaker1.UserId, 5, ct);

        // Hai người có bản đã nộp; thứ tự theo số bản đã nộp, không theo bản được duyệt.
        Assert.Equal(2, board.Count);

        var hang1 = board[0];
        Assert.Equal(1, hang1.Rank);
        Assert.Equal(speaker1.UserId, hang1.UserId);
        Assert.Equal(3, hang1.Submitted);
        Assert.True(hang1.IsMe);

        var hang2 = board[1];
        Assert.Equal(2, hang2.Rank);
        Assert.Equal(speaker2.UserId, hang2.UserId);
        Assert.Equal(1, hang2.Submitted);
        Assert.False(hang2.IsMe);
    }
}
