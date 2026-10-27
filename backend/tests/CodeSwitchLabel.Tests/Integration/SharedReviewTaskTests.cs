using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Reviews;
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
        string recordingId, long reviewerId, long taskId, int vong, CancellationToken ct,
        ReviewDecision quyetDinh = ReviewDecision.Approved) =>
        ReviewService.SubmitAsync(recordingId, reviewerId, new SubmitReviewRequest
        {
            Decision = quyetDinh,
            ExpectedRound = vong,
            TaskId = taskId,
            RejectionReasonCodes = quyetDinh == ReviewDecision.Rejected ? ["background_noise"] : []
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

        // Phiếu thứ hai NGƯỢC phiếu đầu: 1-1 thì chưa bên nào quá bán, nên mới có vòng ba.
        var vong2 = await DuyetAsync(
            recording.RecordingId, rv2.UserId, task.TaskId, 2, ct, ReviewDecision.Rejected);
        Assert.Equal(2, vong2.Round);
        Assert.False(vong2.IsFinal);
        Assert.Equal(TaskRecordingStatus.Queued, await TrangThaiLoAsync(task.TaskId, recording.RecordingId, ct));

        var vong3 = await DuyetAsync(recording.RecordingId, rv3.UserId, task.TaskId, 3, ct);

        Assert.Equal(3, vong3.Round);
        Assert.True(vong3.IsFinal, "Hai phiếu đạt trên ba vòng là quá bán, trigger phải chốt.");
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
    /// Hai phiếu giống nhau trên ba vòng là quá bán — chốt NGAY, không bắt người đọc chờ lượt
    /// thứ ba vốn không lật được kết quả. Lô cũng xong theo, và lượt thứ ba bị từ chối.
    /// </summary>
    [Fact]
    public async Task HaiPhieuTuChoi_ChotTruotNgay_KhongChoLuotThuBa()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var (manager, campaign) = await TaoDotAsync(dau, ct);
        var speaker = await CreateUserAsync($"sp-{dau}@test.local", RoleName.Speaker);
        var rv1 = await CreateUserAsync($"rv1-{dau}@test.local", RoleName.Reviewer);
        var rv2 = await CreateUserAsync($"rv2-{dau}@test.local", RoleName.Reviewer);
        var rv3 = await CreateUserAsync($"rv3-{dau}@test.local", RoleName.Reviewer);

        var script = await CreateValidatedScriptAsync(
            $"[vi]Tối nay bên kỹ thuật sẽ [en]update{dau} [vi]máy chủ.",
            $"[vi]Tối nay bên kỹ thuật sẽ cập nhật máy chủ {dau}.",
            ScriptDomain.ItTechnology, speaker.UserId);

        var recording = await CreateRecordingAsync(
            script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);

        var task = await TaoLoDuyetAsync(
            campaign.CampaignId, manager.UserId, recording.RecordingId,
            [rv1.UserId, rv2.UserId, rv3.UserId], 1, ct);

        var vong1 = await DuyetAsync(
            recording.RecordingId, rv1.UserId, task.TaskId, 1, ct, ReviewDecision.Rejected);
        Assert.False(vong1.IsFinal);

        var vong2 = await DuyetAsync(
            recording.RecordingId, rv2.UserId, task.TaskId, 2, ct, ReviewDecision.Rejected);

        Assert.True(vong2.IsFinal, "Hai phiếu từ chối trên ba vòng là quá bán.");
        Assert.Equal(RecordingStatus.Rejected, vong2.RecordingStatus);

        // Lô hết việc với bản ghi này, dù người thứ ba chưa bỏ phiếu. Là Skipped chứ không
        // phải Reviewed vì bản ghi bị LOẠI: lượt duyệt của nó không còn đóng góp cho chỉ tiêu
        // của lô nữa (TEAM_010, yêu cầu 4.2 — chỉ tiêu phải tụt khi bản ghi bị loại).
        Assert.Equal(TaskRecordingStatus.Skipped, await TrangThaiLoAsync(task.TaskId, recording.RecordingId, ct));

        // Mỗi request thật có DbContext riêng, còn test dùng chung một cái — không xoá bộ nhớ
        // theo dõi thì service đọc lại đúng thực thể cũ và không thấy trạng thái trigger vừa ghi.
        Db.ChangeTracker.Clear();

        var muon = await Assert.ThrowsAsync<ConflictException>(
            () => DuyetAsync(recording.RecordingId, rv3.UserId, task.TaskId, 3, ct, ReviewDecision.Approved));
        Assert.Equal("recording_not_reviewable", muon.Code);
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

    /// <summary>
    /// `groupBy=pair` gom hai bản của một cặp vào MỘT dòng. Mặc định mỗi bản một dòng, nên màn
    /// hình nào vẽ mỗi dòng thành một thẻ cặp sẽ thấy cặp nào cũng hiện hai lần — đúng chỗ đội
    /// frontend báo. Dòng gom phải mang bản đang cần tay người duyệt, không phải bản bất kỳ.
    /// </summary>
    [Fact]
    public async Task GomTheoCap_MoiCapMotDong_MangBanDangCanDuyet()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var (manager, campaign) = await TaoDotAsync(dau, ct);
        var speaker = await CreateUserAsync($"sp-gom-{dau}@test.local", RoleName.Speaker);
        var reviewer = await CreateUserAsync($"rv-gom-{dau}@test.local", RoleName.Reviewer);

        var task = await CreateTaskAsync(campaign.CampaignId, TaskType.Review, 4,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = task.TaskId,
            UserId = reviewer.UserId,
            AssignmentStatus = AssignmentStatus.Active,
            AssignedAt = DateTimeOffset.UtcNow
        });

        var scriptIds = new List<string>();

        foreach (var i in new[] { 1, 2 })
        {
            var script = await CreateValidatedScriptAsync(
                $"[vi]Cả nhóm đã [en]deploy{dau}{i} [vi]xong rồi.",
                $"[vi]Cả nhóm đã triển khai xong rồi {dau}{i}.",
                ScriptDomain.ItTechnology, speaker.UserId);

            scriptIds.Add(script.ScriptId);

            foreach (var variant in new[] { SentenceVariant.CodeSwitching, SentenceVariant.PureVietnamese })
            {
                var recording = await CreateRecordingAsync(script.ScriptId, speaker.UserId, variant);

                Db.TaskRecordings.Add(new TaskRecording
                {
                    TaskId = task.TaskId,
                    RecordingId = recording.RecordingId,
                    Status = TaskRecordingStatus.Queued
                });
            }
        }

        await Db.SaveChangesAsync(ct);

        // groupBy=recording: hai cặp ra BỐN dòng — hình dạng cũ, giờ phải hỏi đích danh.
        var theoBanGhi = await ReviewService.GetTaskRecordingsAsync(
            task.TaskId, reviewer.UserId,
            new TaskReviewQuery { PageSize = 50, GroupBy = TaskReviewGrouping.Recording }, ct);

        Assert.Equal(4, theoBanGhi.Total);
        Assert.Equal(4, theoBanGhi.Items.Count);
        Assert.Equal(2, theoBanGhi.Items.Select(i => i.ScriptId).Distinct().Count());

        // MẶC ĐỊNH gom theo cặp: hai cặp ra HAI dòng, và tổng cũng đếm theo cặp.
        var theoCap = await ReviewService.GetTaskRecordingsAsync(
            task.TaskId, reviewer.UserId, new TaskReviewQuery { PageSize = 50 }, ct);

        Assert.Equal(2, theoCap.Total);
        Assert.Equal(2, theoCap.Items.Count);
        Assert.Equal([.. scriptIds.OrderBy(x => x)], [.. theoCap.Items.Select(i => i.ScriptId).OrderBy(x => x)]);

        foreach (var dong in theoCap.Items)
        {
            // Dòng mang bản duyệt được ngay, và bản còn lại nằm trong sibling với trạng thái riêng.
            Assert.True(dong.CanReview);
            Assert.Equal(ReviewBlocker.None, dong.Blocker);

            var kia = Assert.IsType<ReviewSiblingDto>(dong.Sibling);
            Assert.NotEqual(dong.SentenceVariant, kia.SentenceVariant);
            Assert.NotEqual(dong.RecordingId, kia.RecordingId);
            Assert.False(kia.MyReviewDone);
            Assert.True(kia.CanReview);
        }
    }

    /// <summary>
    /// Từ chối là từ chối CẢ CẶP: bản còn lại cũng bị loại để người đọc thu lại nguyên cặp.
    /// Ca khó nhất ở đây — bản còn lại ĐÃ ĐẠT rồi vẫn phải bị gỡ, vì cặp chỉ dùng được khi có
    /// đủ hai bản cùng một giọng; giữ lại nửa đã đạt thì nửa mới thu sẽ lệch điều kiện thu.
    /// </summary>
    [Fact]
    public async Task TuChoiMotBan_BanConLaiDuDaDat_CungBiLoaiDeThuLaiCaCap()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var (manager, campaign) = await TaoDotAsync(dau, ct);
        var speaker = await CreateUserAsync($"sp-cap-{dau}@test.local", RoleName.Speaker);
        var rv1 = await CreateUserAsync($"rv1-cap-{dau}@test.local", RoleName.Reviewer);
        var rv2 = await CreateUserAsync($"rv2-cap-{dau}@test.local", RoleName.Reviewer);

        var script = await CreateValidatedScriptAsync(
            $"[vi]Làm ơn mở [en]Teams{dau} [vi]giúp em.", $"[vi]Làm ơn mở ứng dụng giúp em {dau}.",
            ScriptDomain.ItTechnology, speaker.UserId);

        var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);

        var task = await TaoLoDuyetAsync(
            campaign.CampaignId, manager.UserId, cs.RecordingId, [rv1.UserId, rv2.UserId], 2, ct);

        Db.TaskRecordings.Add(new TaskRecording
        {
            TaskId = task.TaskId,
            RecordingId = vi.RecordingId,
            Status = TaskRecordingStatus.Queued
        });
        await Db.SaveChangesAsync(ct);

        // Bản thuần Việt được duyệt ĐẠT trước: hai phiếu là quá bán, chốt ngay.
        await DuyetAsync(vi.RecordingId, rv1.UserId, task.TaskId, 1, ct);
        var viChot = await DuyetAsync(vi.RecordingId, rv2.UserId, task.TaskId, 2, ct);

        Assert.Equal(RecordingStatus.Approved, viChot.RecordingStatus);

        // Rồi bản chen tiếng Anh bị từ chối.
        await DuyetAsync(cs.RecordingId, rv1.UserId, task.TaskId, 1, ct, ReviewDecision.Rejected);
        var csChot = await DuyetAsync(cs.RecordingId, rv2.UserId, task.TaskId, 2, ct, ReviewDecision.Rejected);

        Assert.Equal(RecordingStatus.Rejected, csChot.RecordingStatus);

        // Bản đã đạt bị gỡ theo cặp, có ghi rõ ai gỡ và vì sao.
        Db.ChangeTracker.Clear();
        var viSau = await Db.Recordings.AsNoTracking()
            .SingleAsync(r => r.RecordingId == vi.RecordingId, ct);

        Assert.Equal(RecordingStatus.Rejected, viSau.Status);
        Assert.NotNull(viSau.DiscardedAt);
        Assert.Equal(rv2.UserId, viSau.DiscardedBy);
        Assert.Contains(cs.RecordingId, viSau.DiscardNote!, StringComparison.Ordinal);

        // Câu vẫn còn dùng được — chỉ hai bản thu bị gỡ, không phải loại câu khỏi kho.
        var cauSau = await Db.Scripts.AsNoTracking()
            .SingleAsync(s => s.ScriptId == script.ScriptId, ct);
        Assert.Equal(ScriptStatus.Validated, cauSau.Status);

        // Người đọc nợ lại CẢ HAI bản: không bản nào của cặp còn hiệu lực.
        var conHieuLuc = await Db.Recordings.AsNoTracking()
            .CountAsync(r => r.ScriptId == script.ScriptId &&
                        (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved), ct);
        Assert.Equal(0, conHieuLuc);
    }
}
