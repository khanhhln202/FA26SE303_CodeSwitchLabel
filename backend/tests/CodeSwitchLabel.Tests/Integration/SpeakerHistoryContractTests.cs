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
/// Ba chỗ trên màn hình của Speaker, theo yêu cầu đội frontend ngày 19/10:
///
///   • lịch sử đóng góp trả MỘT lượt duyệt (duyệt văn bản chỉ có một lượt — TEAM_010),
///     không phải mảng ba vòng như bản ghi âm;
///   • lịch sử đề xuất của chính mình — hàng chờ của Reviewer chỉ có đề xuất đang chờ nên
///     đề xuất đã xử lý không còn chỗ nào xem lại;
///   • bảng xếp hạng kèm chỉ tiêu riêng từng người, để vẽ được "15/100".
/// </summary>
[Trait("Category", "Integration")]
[Collection("Database")]
public sealed class SpeakerHistoryContractTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private ISpeakerRoundsService Rounds =>
        Scope.ServiceProvider.GetRequiredService<ISpeakerRoundsService>();

    private static string Dau() => Guid.NewGuid().ToString("N")[..8];

    private Task<short> ReasonIdAsync(string code, CancellationToken ct) =>
        Db.ScriptErrorReasons.AsNoTracking()
            .Where(r => r.ReasonCode == code)
            .Select(r => r.ReasonId)
            .FirstAsync(ct);

    // ------------------------------------------------------------------ lịch sử đóng góp

    /// <summary>
    /// Câu đã có kết quả thì `review` mang đủ thứ màn hình cần: ai duyệt, lúc nào, lý do lỗi
    /// và lời nhắn. Câu còn chờ thì `review` trống — không phải mảng rỗng.
    /// </summary>
    [Fact]
    public async Task LichSuDongGop_TraMotLuotDuyet_KemTenNguoiDuyetVaLyDo()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var speaker = await CreateUserAsync($"sp-ls-{dau}@test.local", RoleName.Speaker);
        var reviewer = await CreateUserAsync($"rv-ls-{dau}@test.local", RoleName.Reviewer, "Nguyễn Người Duyệt");

        var daXuLy = await CreateScriptAsync(
            $"[vi]Em nên [en]scan{dau} [vi]tài liệu này.", $"[vi]Em nên quét tài liệu này {dau}.",
            ScriptDomain.ItTechnology, speaker.UserId);

        var conCho = await CreateScriptAsync(
            $"[vi]Mình gửi [en]email{dau} [vi]cho khách nhé.", $"[vi]Mình gửi thư cho khách nhé {dau}.",
            ScriptDomain.ItTechnology, speaker.UserId);

        Db.ScriptReviews.Add(new ScriptReview
        {
            ScriptId = daXuLy.ScriptId,
            UserId = reviewer.UserId,
            Action = ScriptReviewAction.Rejected,
            ErrorReasonId = await ReasonIdAsync("unnatural", ct),
            Comment = "Câu đọc chưa tự nhiên.",
            ReviewedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(ct);

        var trang = await Rounds.GetContributionHistoryAsync(
            speaker.UserId, new SpeakerContributionHistoryQuery { PageSize = 50 }, ct);

        var dong = Assert.Single(trang.Items, i => i.ScriptId == daXuLy.ScriptId);
        var duyet = Assert.IsType<ContributionReviewDto>(dong.Review);

        Assert.Equal("Rejected", duyet.Action);
        Assert.Equal(reviewer.UserId, duyet.ReviewerId);
        Assert.Equal("Nguyễn Người Duyệt", duyet.ReviewerName);
        Assert.Equal("unnatural", duyet.ErrorReasonCode);
        Assert.False(string.IsNullOrWhiteSpace(duyet.ErrorReasonDescription));
        Assert.Equal("Câu đọc chưa tự nhiên.", duyet.Comment);
        Assert.True(duyet.ReviewId > 0);

        Assert.Null(Assert.Single(trang.Items, i => i.ScriptId == conCho.ScriptId).Review);
    }

    /// <summary>
    /// Vài câu cũ còn hai lượt duyệt từ trước khi siết luật một-lượt. Lấy lượt MỚI NHẤT,
    /// vì đó là kết quả đang có hiệu lực của câu.
    /// </summary>
    [Fact]
    public async Task LichSuDongGop_CauCoHaiLuotCu_LayLuotMoiNhat()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var speaker = await CreateUserAsync($"sp-hai-{dau}@test.local", RoleName.Speaker);
        var cu = await CreateUserAsync($"rv-cu-{dau}@test.local", RoleName.Reviewer, "Người Duyệt Trước");
        var moi = await CreateUserAsync($"rv-moi-{dau}@test.local", RoleName.Reviewer, "Người Duyệt Sau");

        var script = await CreateScriptAsync(
            $"[vi]Chiều nay họp [en]online{dau} [vi]nhé.", $"[vi]Chiều nay họp trực tuyến nhé {dau}.",
            ScriptDomain.ItTechnology, speaker.UserId);

        Db.ScriptReviews.Add(new ScriptReview
        {
            ScriptId = script.ScriptId,
            UserId = cu.UserId,
            Action = ScriptReviewAction.Rejected,
            // ck_script_review_rejection_reason: từ chối thì bắt buộc có lý do lỗi.
            ErrorReasonId = await ReasonIdAsync("unnatural", ct),
            Comment = "Lượt cũ",
            ReviewedAt = DateTimeOffset.UtcNow.AddHours(-2)
        });
        Db.ScriptReviews.Add(new ScriptReview
        {
            ScriptId = script.ScriptId,
            UserId = moi.UserId,
            Action = ScriptReviewAction.Accepted,
            Comment = "Lượt mới",
            ReviewedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(ct);

        var trang = await Rounds.GetContributionHistoryAsync(
            speaker.UserId, new SpeakerContributionHistoryQuery { PageSize = 50 }, ct);

        var duyet = Assert.Single(trang.Items, i => i.ScriptId == script.ScriptId).Review;

        Assert.NotNull(duyet);
        Assert.Equal("Người Duyệt Sau", duyet.ReviewerName);
        Assert.Equal("Accepted", duyet.Action);
    }

    // ------------------------------------------------------------------ lịch sử đề xuất

    /// <summary>
    /// Đường chính: Speaker xem lại đề xuất của CHÍNH MÌNH, mọi trạng thái, mới nhất trước,
    /// kèm kết quả xử lý. Đề xuất của người khác không lọt vào.
    /// </summary>
    [Fact]
    public async Task LichSuDeXuat_ChiCuaMinh_MoiNhatTruoc_KemKetQua()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var toi = await CreateUserAsync($"sp-dx-{dau}@test.local", RoleName.Speaker);
        var nguoiKhac = await CreateUserAsync($"sp-dx2-{dau}@test.local", RoleName.Speaker);
        var reviewer = await CreateUserAsync($"rv-dx-{dau}@test.local", RoleName.Reviewer, "Trần Người Chốt");

        var cauBiTuChoi = await CreateValidatedScriptAsync(
            $"[vi]Anh gửi em cái [en]link{dau} [vi]nhé.", $"[vi]Anh gửi em cái đường dẫn nhé {dau}.",
            ScriptDomain.ItTechnology, toi.UserId);

        var cauConCho = await CreateValidatedScriptAsync(
            $"[vi]Em [en]deploy{dau} [vi]xong rồi.", $"[vi]Em triển khai xong rồi {dau}.",
            ScriptDomain.ItTechnology, toi.UserId);

        var cauNguoiKhac = await CreateValidatedScriptAsync(
            $"[vi]Chị đặt [en]meeting{dau} [vi]giúp em.", $"[vi]Chị đặt buổi họp giúp em {dau}.",
            ScriptDomain.ItTechnology, nguoiKhac.UserId);

        // Đề xuất đầu: báo lỗi, bị Reviewer từ chối.
        var daTuChoi = await ProposalService.CreateAsync(cauBiTuChoi.ScriptId, toi.UserId, new CreateProposalRequest
        {
            Kind = ProposalKind.Report,
            ErrorReasonCode = "unnatural",
            Comment = "Câu nghe cứng."
        }, ct);

        await ProposalService.ResolveAsync(daTuChoi.ProposalId, reviewer.UserId, new ResolveProposalRequest
        {
            Decision = ProposalDecision.Rejected,
            Comment = "Câu vẫn ổn, giữ nguyên."
        }, ct);

        // Lời giải gốc của câu — bản sửa phải phủ đủ từ tiếng Anh, nên câu cần có sẵn dòng này.
        Db.ScriptWords.Add(new ScriptWord
        {
            ScriptId = cauConCho.ScriptId,
            WordPosition = 1,
            EnWord = $"deploy{dau}",
            ViWord = "triển khai",
            Relation = ScriptWordRelation.SemanticEquivalent
        });
        await Db.SaveChangesAsync(ct);

        // Đề xuất sau: kèm bản sửa, còn đang chờ.
        var dangCho = await ProposalService.CreateAsync(cauConCho.ScriptId, toi.UserId, new CreateProposalRequest
        {
            Kind = ProposalKind.Edit,
            ProposedCsContent = $"[vi]Em đã [en]deploy{dau} [vi]xong rồi.",
            ProposedViContent = $"[vi]Em đã triển khai xong rồi {dau}.",
            ProposedAlignment = [new AlignmentItem { Source = $"deploy{dau}", Target = "triển khai" }],
            Comment = "Thêm chữ đã cho rõ."
        }, ct);

        var cuaNguoiKhac = await ProposalService.CreateAsync(
            cauNguoiKhac.ScriptId, nguoiKhac.UserId, new CreateProposalRequest
            {
                Kind = ProposalKind.Report,
                ErrorReasonCode = "mismatch"
            }, ct);

        var trang = await ProposalService.ListForProposerAsync(
            toi.UserId, new SpeakerProposalHistoryQuery { PageSize = 50 }, ct);

        Assert.Equal(2, trang.Total);
        Assert.DoesNotContain(trang.Items, i => i.ProposalId == cuaNguoiKhac.ProposalId);

        // Mới nhất trước — ngược chiều hàng chờ của Reviewer.
        Assert.Equal(dangCho.ProposalId, trang.Items[0].ProposalId);

        var moi = trang.Items[0];
        Assert.Equal(ProposalKind.Edit, moi.Kind);
        Assert.Equal(ProposalStatus.Pending, moi.Status);
        Assert.Null(moi.DecidedBy);
        Assert.Null(moi.DecidedByName);
        Assert.Null(moi.DecidedAt);
        Assert.NotNull(moi.Proposed);
        Assert.Contains("Em đã", moi.Proposed.CsContent, StringComparison.Ordinal);
        Assert.Equal(cauConCho.ScriptId, moi.ScriptId);

        var cu = trang.Items[1];
        Assert.Equal(daTuChoi.ProposalId, cu.ProposalId);
        Assert.Equal(ProposalStatus.Rejected, cu.Status);
        Assert.Equal(reviewer.UserId, cu.DecidedBy);
        Assert.Equal("Trần Người Chốt", cu.DecidedByName);
        Assert.NotNull(cu.DecidedAt);
        Assert.Equal("Câu vẫn ổn, giữ nguyên.", cu.DecisionNote);
        Assert.Equal("unnatural", cu.ErrorReasonCode);
        Assert.Equal("Câu nghe cứng.", cu.Comment);

        // Báo lỗi không kèm bản sửa — Reviewer tự đưa bản chốt khi duyệt.
        Assert.Null(cu.Proposed);
        Assert.Contains(dau, cu.Original.CsContent, StringComparison.Ordinal);

        // Lọc theo trạng thái và loại — hai bộ lọc duy nhất của màn hình.
        var chiCho = await ProposalService.ListForProposerAsync(
            toi.UserId, new SpeakerProposalHistoryQuery { Status = ProposalStatus.Pending, PageSize = 50 }, ct);
        Assert.Equal(dangCho.ProposalId, Assert.Single(chiCho.Items).ProposalId);

        var chiBaoLoi = await ProposalService.ListForProposerAsync(
            toi.UserId, new SpeakerProposalHistoryQuery { Kind = ProposalKind.Report, PageSize = 50 }, ct);
        Assert.Equal(daTuChoi.ProposalId, Assert.Single(chiBaoLoi.Items).ProposalId);
    }

    // ------------------------------------------------------------------ chỉ tiêu riêng

    /// <summary>
    /// Mỗi dòng xếp hạng mang chỉ tiêu RIÊNG của người đó: tổng quota trên các lượt giao task
    /// thu âm họ đang nhận trong đợt. Cùng một task giao nhiều người thì mỗi người nhận quota
    /// của mình, không lấy chỉ tiêu chung của task.
    ///
    /// Số đã nộp vẫn là số THẬT (hệ thống cho thu vượt chỉ tiêu, xem WorkAfterTargetTests);
    /// bản đã chặn ở mốc nằm riêng ở `submittedCapped` để giao diện không vẽ "4/3".
    /// </summary>
    [Fact]
    public async Task BangXepHang_ChiTieuRieng_ChanOMocCuaTungNguoi()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var manager = await CreateUserAsync($"tm-ct-{dau}@test.local", RoleName.TaskManager);
        // ck_campaign_target_qty: chỉ tiêu đợt phải nằm trong 2000–5000.
        var campaign = await CreateCampaignAsync($"Đợt chỉ tiêu {dau}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), manager.UserId);

        // Đợt phải có Task Manager phụ trách trước khi tạo nhiệm vụ (trigger của lược đồ).
        campaign.AssignedTo = manager.UserId;
        await Db.SaveChangesAsync(ct);

        // Người thứ nhất nhận hai task: quota 1 + 2 = 3.
        var taskA = await CreateTaskAsync(campaign.CampaignId, TaskType.Recording, 1,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));
        var taskB = await CreateTaskAsync(campaign.CampaignId, TaskType.Recording, 3,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        var vuot = await CreateUserAsync($"sp-vuot-{dau}@test.local", RoleName.Speaker);
        var duoi = await CreateUserAsync($"sp-duoi-{dau}@test.local", RoleName.Speaker);
        var khongNhan = await CreateUserAsync($"sp-khong-{dau}@test.local", RoleName.Speaker);

        foreach (var (task, user, quota) in new[]
                 {
                     (taskA, vuot, 1),
                     // Task B chia cho hai người với quota khác nhau: 2 và 1.
                     (taskB, vuot, 2),
                     (taskB, duoi, 1)
                 })
        {
            Db.TaskAssignments.Add(new TaskAssignment
            {
                TaskId = task.TaskId,
                UserId = user.UserId,
                TargetQty = quota,
                AssignmentStatus = AssignmentStatus.Active,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        await NopAsync(vuot.UserId, taskA.TaskId, 4, $"v{dau}", ct);
        await NopAsync(duoi.UserId, taskB.TaskId, 1, $"d{dau}", ct);
        await NopAsync(khongNhan.UserId, taskB.TaskId, 2, $"k{dau}", ct);

        var bang = await Rounds.GetLeaderboardAsync(campaign.CampaignId, vuot.UserId, 10, ct);

        // Thứ hạng theo số đã nộp THẬT: 4 > 2 > 1, không theo số đã chặn ở quota.
        Assert.Equal(vuot.UserId, bang.Items[0].UserId);
        Assert.Equal(1, bang.Items[0].Rank);
        Assert.Equal(khongNhan.UserId, bang.Items[1].UserId);
        Assert.Equal(duoi.UserId, bang.Items[2].UserId);

        var dongVuot = Assert.Single(bang.Items, i => i.UserId == vuot.UserId);
        Assert.Equal(3, dongVuot.TargetQty);
        Assert.Equal(4, dongVuot.Submitted);
        Assert.Equal(3, dongVuot.SubmittedCapped);

        var dongDuoi = Assert.Single(bang.Items, i => i.UserId == duoi.UserId);
        Assert.Equal(1, dongDuoi.TargetQty);
        Assert.Equal(1, dongDuoi.Submitted);
        Assert.Equal(1, dongDuoi.SubmittedCapped);

        // Có bản ghi trong đợt nhưng không giữ lượt giao nào: chỉ tiêu 0. Chặn ở 0 sẽ ra "0/0"
        // nên giữ số thật — giao diện tự quyết hiển thị thế nào.
        var dongKhongNhan = Assert.Single(bang.Items, i => i.UserId == khongNhan.UserId);
        Assert.Equal(0, dongKhongNhan.TargetQty);
        Assert.Equal(2, dongKhongNhan.SubmittedCapped);

        // Mốc cả đợt không đổi: tổng chỉ tiêu đợt và tổng chỉ tiêu nhiệm vụ đã chia.
        Assert.Equal(2000, bang.CampaignTargetQty);
        Assert.Equal(4, bang.AllocatedTargetQty);
    }

    /// <summary>
    /// Trang chủ lấy `target` từ tổng quota cá nhân trên các task trong đợt, không lấy
    /// `campaign.targetQty` (2000). Lượt giao cũ chưa có quota vẫn chia theo số người nhận,
    /// đúng quy tắc của API tiến độ Speaker.
    /// </summary>
    [Fact]
    public async Task DotHienTai_ChiTieuLaTongQuotaCaNhan_KhongPhaiChiTieuChienDich()
    {
        var ct = TestContext.Current.CancellationToken;
        var dau = Dau();

        var manager = await CreateUserAsync($"tm-dot-{dau}@test.local", RoleName.TaskManager);
        var campaign = await CreateCampaignAsync($"Đợt trang chủ {dau}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), manager.UserId);
        campaign.AssignedTo = manager.UserId;
        await Db.SaveChangesAsync(ct);

        var taskA = await CreateTaskAsync(campaign.CampaignId, TaskType.Recording, 4,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));
        var taskB = await CreateTaskAsync(campaign.CampaignId, TaskType.Recording, 6,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));
        // Task cũ: chưa có quota trên lượt giao, phải chia đều theo số người nhận.
        var taskC = await CreateTaskAsync(campaign.CampaignId, TaskType.Recording, 3,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        var toi = await CreateUserAsync($"sp-dot-{dau}@test.local", RoleName.Speaker, $"A {dau}");
        var nguoiKhac = await CreateUserAsync($"sp-dot-k-{dau}@test.local", RoleName.Speaker, $"B {dau}");

        foreach (var (task, user, quota) in new (WorkTask, AppUser, int?)[]
                 {
                     (taskA, toi, 3), (taskA, nguoiKhac, 1),
                     (taskB, toi, 6),
                     (taskC, toi, null), (taskC, nguoiKhac, null)
                 })
        {
            Db.TaskAssignments.Add(new TaskAssignment
            {
                TaskId = task.TaskId,
                UserId = user.UserId,
                TargetQty = quota,
                AssignmentStatus = AssignmentStatus.Active,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        await Db.SaveChangesAsync(ct);

        var dot = await Rounds.GetCurrentAsync(toi.UserId, ct);

        Assert.NotNull(dot);
        // 3 + 6 + 2 (phần chia của task cũ) — không phải 2000 của cả chiến dịch.
        Assert.Equal(11, dot.Target);
        Assert.Equal(0, dot.Submitted);
    }

    /// <summary>Nộp <paramref name="soBan"/> bản ghi của một người vào một task.</summary>
    private async Task NopAsync(long speakerId, long taskId, int soBan, string nhan, CancellationToken ct)
    {
        for (var i = 0; i < soBan; i++)
        {
            var script = await CreateValidatedScriptAsync(
                $"[vi]Em [en]{nhan}{i} [vi]đọc câu này.", $"[vi]Em đọc câu này {nhan}{i}.",
                ScriptDomain.ItTechnology, speakerId);

            var recording = await CreateRecordingAsync(
                script.ScriptId, speakerId, SentenceVariant.CodeSwitching);
            recording.TaskId = taskId;
        }

        await Db.SaveChangesAsync(ct);
    }
}
