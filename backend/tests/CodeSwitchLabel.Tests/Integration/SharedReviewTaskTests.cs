using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Lô duyệt giao cho NHIỀU người, và lượt duyệt hiện trong lịch sử của người đọc.
///
/// Bản ghi cần ba lượt duyệt độc lập. Trước đây dòng task_recording bị đánh 'reviewed' ngay
/// lượt đầu, nên lô giao cho ba Reviewer thì người thứ hai gửi kèm taskId sẽ bị chặn — bản ghi
/// kẹt ở 1/3 và không bao giờ chốt, trong khi danh sách của chính lô đó vẫn liệt kê bản ghi là
/// duyệt được. Luật đúng: lô chỉ xong với một bản ghi khi không còn ai trong lô duyệt được nữa.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Database")]
public sealed class SharedReviewTaskTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private ISpeakerRoundsService Rounds =>
        Scope.ServiceProvider.GetRequiredService<ISpeakerRoundsService>();

    private static string Dau() => Guid.NewGuid().ToString("N")[..8];

    private Task<TaskRecordingStatus> TrangThaiLoAsync(long taskId, string recordingId, CancellationToken ct) =>
        Db.TaskRecordings.AsNoTracking()
            .Where(tr => tr.TaskId == taskId && tr.RecordingId == recordingId)
            .Select(tr => tr.Status)
            .SingleAsync(ct);

    /// <summary>Đợt + Task Manager phụ trách — lược đồ không cho tạo nhiệm vụ khi đợt chưa có ai phụ trách.</summary>
    private async Task<(AppUser Manager, Campaign Campaign)> TaoDotAsync(string dau, CancellationToken ct)
    {
        var manager = await CreateUserAsync($"tm-{dau}@test.local", RoleName.TaskManager);

        var campaign = await CreateCampaignAsync($"Đợt duyệt {dau}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), manager.UserId);

        campaign.AssignedTo = manager.UserId;
        await Db.SaveChangesAsync(ct);

        return (manager, campaign);
    }

    private async Task<WorkTask> TaoLoDuyetAsync(
        long campaignId, long managerId, string recordingId, long[] reviewerIds,
        int targetQty, CancellationToken ct)
    {
        var task = await CreateTaskAsync(campaignId, TaskType.Review, targetQty,
            managerId, DateTimeOffset.UtcNow.AddDays(10));

        Db.TaskRecordings.Add(new TaskRecording
        {
            TaskId = task.TaskId,
            RecordingId = recordingId,
            Status = TaskRecordingStatus.Queued
        });

        foreach (var reviewerId in reviewerIds)
        {
            Db.TaskAssignments.Add(new TaskAssignment
            {
                TaskId = task.TaskId,
                UserId = reviewerId,
                AssignmentStatus = AssignmentStatus.Active,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        await Db.SaveChangesAsync(ct);
        return task;
    }

    private Task<SubmitReviewResult> DuyetAsync(
        string recordingId, long reviewerId, long taskId, int vong, CancellationToken ct) =>
        ReviewService.SubmitAsync(recordingId, reviewerId, new SubmitReviewRequest
        {
            Decision = ReviewDecision.Approved,
            ExpectedRound = vong,
            TaskId = taskId
        }, ct);

    /// <summary>
    /// Ba Reviewer cùng một lô duyệt đủ ba vòng cho một bản ghi. Đây chính là tình huống đội
    /// frontend gặp: 0/3 thì ai duyệt cũng được, 1/3 thì những người còn lại bị báo lỗi.
    /// </summary>
    [Fact]
    public async Task BaNguoiMotLo_DuyetDuDuocBaVong_LoChiXongODuoiCung()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var (manager, campaign) = await TaoDotAsync(dau, ct);
        var speaker = await CreateUserAsync($"sp-{dau}@test.local", RoleName.Speaker);

        var rv1 = await CreateUserAsync($"rv1-{dau}@test.local", RoleName.Reviewer);
        var rv2 = await CreateUserAsync($"rv2-{dau}@test.local", RoleName.Reviewer);
        var rv3 = await CreateUserAsync($"rv3-{dau}@test.local", RoleName.Reviewer);

        var script = await CreateValidatedScriptAsync(
            $"[vi]Em nhớ [en]upload{dau} [vi]tài liệu nhé.", $"[vi]Em nhớ tải tài liệu lên nhé {dau}.",
            ScriptDomain.ItTechnology, speaker.UserId);

        var recording = await CreateRecordingAsync(
            script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);

        var task = await TaoLoDuyetAsync(
            campaign.CampaignId, manager.UserId, recording.RecordingId,
            [rv1.UserId, rv2.UserId, rv3.UserId], 1, ct);

        var vong1 = await DuyetAsync(recording.RecordingId, rv1.UserId, task.TaskId, 1, ct);
        Assert.Equal(1, vong1.Round);
        Assert.False(vong1.IsFinal);

        // Mấu chốt: lô vẫn còn chờ, vì hai người kia chưa bỏ phiếu.
        Assert.Equal(TaskRecordingStatus.Queued, await TrangThaiLoAsync(task.TaskId, recording.RecordingId, ct));

        var vong2 = await DuyetAsync(recording.RecordingId, rv2.UserId, task.TaskId, 2, ct);
        Assert.Equal(2, vong2.Round);
        Assert.Equal(TaskRecordingStatus.Queued, await TrangThaiLoAsync(task.TaskId, recording.RecordingId, ct));

        var vong3 = await DuyetAsync(recording.RecordingId, rv3.UserId, task.TaskId, 3, ct);

        Assert.Equal(3, vong3.Round);
        Assert.True(vong3.IsFinal, "Đủ ba lượt thì trigger phải chốt bản ghi.");
        Assert.Equal(RecordingStatus.Approved, vong3.RecordingStatus);

        // Hết người duyệt được và bản ghi đã chốt — giờ lô mới xong với bản ghi này.
        Assert.Equal(TaskRecordingStatus.Reviewed, await TrangThaiLoAsync(task.TaskId, recording.RecordingId, ct));
    }

    /// <summary>
    /// Lô giao cho MỘT người thì không đổi so với trước: người đó duyệt xong là lô hết việc
    /// với bản ghi đó, dù bản ghi còn chờ hai lượt nữa từ nơi khác.
    /// </summary>
    [Fact]
    public async Task MotNguoiMotLo_DuyetXongLaLoHetViecVoiBanGhiDo()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var (manager, campaign) = await TaoDotAsync(dau, ct);
        var speaker = await CreateUserAsync($"sp-{dau}@test.local", RoleName.Speaker);
        var reviewer = await CreateUserAsync($"rv-{dau}@test.local", RoleName.Reviewer);

        var script = await CreateValidatedScriptAsync(
            $"[vi]Chiều nay nhóm mình [en]meeting{dau} [vi]nhé.", $"[vi]Chiều nay nhóm mình họp nhé {dau}.",
            ScriptDomain.ItTechnology, speaker.UserId);

        var recording = await CreateRecordingAsync(
            script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);

        var task = await TaoLoDuyetAsync(
            campaign.CampaignId, manager.UserId, recording.RecordingId, [reviewer.UserId], 1, ct);

        var ketQua = await DuyetAsync(recording.RecordingId, reviewer.UserId, task.TaskId, 1, ct);

        Assert.False(ketQua.IsFinal);
        Assert.Equal(TaskRecordingStatus.Reviewed, await TrangThaiLoAsync(task.TaskId, recording.RecordingId, ct));
    }

    /// <summary>
    /// Lịch sử ghi âm hiện lượt duyệt NGAY, không chờ bản ghi chốt — và lượt từ chối không kèm
    /// bình luận vẫn có lý do để hiện (ghép từ mã lý do đã chọn).
    /// </summary>
    [Fact]
    public async Task LichSuGhiAm_HienLuotDuyetKhiChuaChot_KemLyDoVaSoVong()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var (manager, campaign) = await TaoDotAsync(dau, ct);
        var speaker = await CreateUserAsync($"sp-{dau}@test.local", RoleName.Speaker);
        var reviewer = await CreateUserAsync($"rv-{dau}@test.local", RoleName.Reviewer);

        var script = await CreateValidatedScriptAsync(
            $"[vi]Nhớ gửi [en]report{dau} [vi]trước hạn nhé.", $"[vi]Nhớ gửi báo cáo trước hạn nhé {dau}.",
            ScriptDomain.ItTechnology, speaker.UserId);

        var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);

        var task = await TaoLoDuyetAsync(
            campaign.CampaignId, manager.UserId, cs.RecordingId, [reviewer.UserId], 2, ct);

        // Từ chối KHÔNG kèm bình luận: lý do phải lấy từ mã đã chọn, nếu không FE nhận null.
        await ReviewService.SubmitAsync(cs.RecordingId, reviewer.UserId, new SubmitReviewRequest
        {
            Decision = ReviewDecision.Rejected,
            ExpectedRound = 1,
            RejectionReasonCodes = ["background_noise"],
            TaskId = task.TaskId
        }, ct);

        var trang = await Rounds.GetRecordingHistoryAsync(
            speaker.UserId, new SpeakerHistoryQuery { PageSize = 50 }, ct);

        var dong = Assert.Single(trang.Items, i => i.ScriptId == script.ScriptId);

        Assert.Equal(3, dong.RoundsRequired);

        // Bản vi chưa ai duyệt, nên tiến độ của CẶP vẫn là 0 — cặp chỉ xong khi cả hai bản xong.
        Assert.Equal(0, dong.ReviewedRounds);

        var luot = Assert.Single(dong.Reviews);
        Assert.Equal(1, luot.Round);
        Assert.Equal("Rejected", luot.Decision);
        Assert.Equal(cs.RecordingId, luot.RecordingId);
        Assert.NotNull(luot.ReviewedAt);
        Assert.False(string.IsNullOrWhiteSpace(luot.Reason), "Từ chối không có bình luận thì phải ghép lý do đã chọn.");

        // Bản ghi vẫn đang chờ hai lượt nữa — lượt duyệt hiện ra không có nghĩa là đã chốt.
        Assert.Equal(RecordingStatus.PendingReview, dong.Status);
        Assert.Equal(vi.RecordingId, dong.ViRecordingId);
    }
}
