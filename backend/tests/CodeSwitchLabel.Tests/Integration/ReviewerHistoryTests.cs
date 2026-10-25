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
/// Lịch sử duyệt của Reviewer: mỗi dòng lịch sử bản ghi là MỘT CẶP (như lịch sử ghi âm
/// của Speaker) — gom hai biến thể trước khi cắt trang, `myDecision` gộp "có lượt từ chối
/// là từ chối". Sau khi người xem đã bỏ phiếu, lịch sử lộ đủ phiếu của cả hai bên kể cả
/// khi cặp chưa chốt; màn hình duyệt đang hoạt động vẫn là duyệt mù. Lịch sử duyệt câu
/// thêm người gửi, cặp từ Anh-Việt và lý do khi lượt của mình là Từ chối.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ReviewerHistoryTests : IntegrationTestBase
{
    private readonly IReviewerStatsService _stats;

    public ReviewerHistoryTests(DatabaseFixture fixture) : base(fixture)
    {
        _stats = Services.GetRequiredService<IReviewerStatsService>();
    }

    [Fact]
    public async Task DuyetHaiBanCuaMotCap_LichSuRaMotDongKemHaiBan()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("hist"), RoleName.Speaker, fullName: "Người đọc một cặp");
        var script = await CreateScriptAsync(
            "[vi]Mai [en]deploy [vi]bản mới nhé.", "[vi]Mai triển khai bản mới nhé.",
            ScriptDomain.ItTechnology, AdminUserId);

        var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching, 4.5m);
        var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese, 3.5m);

        // Tôi duyệt cả hai bản của cặp, mỗi bản một lượt.
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(cs.RecordingId).WithReviewerId(ReviewerUserId).WithRound(1).Build());
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(vi.RecordingId).WithReviewerId(ReviewerUserId).WithRound(1).Build());
        await Db.SaveChangesAsync(ct);

        var page = await _stats.GetHistoryAsync(
            ReviewerUserId, new ReviewerHistoryQuery { PageSize = 50 }, ct);

        // Hai lượt duyệt của tôi chỉ ra MỘT cặp trong lịch sử.
        Assert.Equal(1, page.Total);
        var item = Assert.Single(page.Items);

        Assert.Equal(script.ScriptId, item.ScriptId);
        Assert.Equal(speaker.UserId, item.SpeakerId);
        Assert.Equal("Người đọc một cặp", item.SpeakerName);
        Assert.Equal(script.CsContent, item.CsText);
        Assert.Equal(script.ViContent, item.ViText);
        Assert.Equal(cs.RecordingId, item.CsRecordingId);
        Assert.Equal(vi.RecordingId, item.ViRecordingId);
        Assert.NotNull(item.CsAudioUrl);
        Assert.NotNull(item.ViAudioUrl);
        Assert.Equal(4.5m, item.DurationSec);
        Assert.Equal("Approved", item.MyDecision);
        Assert.Equal(RecordingStatus.PendingReview, item.RecordingStatus);

        // Chưa bản nào chốt nên chỉ thấy phiếu của CHÍNH MÌNH — mỗi bản một lượt.
        Assert.Equal(2, item.Reviews.Count);
        Assert.Equal([cs.RecordingId, vi.RecordingId], item.Reviews.Select(r => r.RecordingId).ToArray());
        Assert.All(item.Reviews, r => Assert.Equal("Approved", r.Decision));
        Assert.Equal(1, item.ReviewedRounds);
        Assert.Equal(3, item.RoundsRequired);
    }

    [Fact]
    public async Task BanDaChot_ChiHienReviewsDaChot_ReasonLayTuBinhLuanHoacMaLyDo()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("hist"), RoleName.Speaker, fullName: "Người đọc bị từ chối");
        var otherA = await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Người duyệt A");
        var otherB = await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Người duyệt B");

        var script = await CreateScriptAsync(
            "[vi]Cho tôi [en]check lại nhé.", "[vi]Cho tôi kiểm tra lại nhé.",
            ScriptDomain.DailyLife, AdminUserId);

        var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);

        // Vòng 1 là lượt CỦA TÔI: từ chối, không bình luận, chỉ gắn mã lý do ở dưới.
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(cs.RecordingId).WithReviewerId(ReviewerUserId).WithRound(1)
            .WithDecision(ReviewDecision.Rejected).Build());
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(cs.RecordingId).WithReviewerId(otherA.UserId).WithRound(2)
            .WithDecision(ReviewDecision.Rejected).WithComment("Tiếng ồn lớn, nghe rõ tiếng quạt.").Build());
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(cs.RecordingId).WithReviewerId(otherB.UserId).WithRound(3)
            .WithDecision(ReviewDecision.Rejected).Build());

        // Bản vi: tôi ĐẠT — quyết định của cặp phải theo luật "có lượt từ chối là từ chối".
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(vi.RecordingId).WithReviewerId(ReviewerUserId).WithRound(1).Build());
        await Db.SaveChangesAsync(ct);

        // Ba lượt từ chối chốt bản cs thành Rejected qua trigger đa số.
        await Db.Entry(cs).ReloadAsync(ct);
        Assert.Equal(RecordingStatus.Rejected, cs.Status);

        // Lượt của tôi thêm mã lý do để phép suy lý do fallback có dữ liệu.
        var backgroundNoise = await Db.RejectionReasons
            .FirstAsync(r => r.ReasonCode == "background_noise", ct);
        var mineId = await Db.Reviews
            .Where(v => v.RecordingId == cs.RecordingId && v.ReviewerId == ReviewerUserId)
            .Select(v => v.ReviewId)
            .SingleAsync(ct);
        Db.ReviewRejectionReasons.Add(new ReviewRejectionReason
        {
            ReviewId = mineId,
            ReasonId = backgroundNoise.ReasonId
        });
        await Db.SaveChangesAsync(ct);

        var page = await _stats.GetHistoryAsync(
            ReviewerUserId, new ReviewerHistoryQuery { PageSize = 50 }, ct);

        var item = Assert.Single(page.Items);

        Assert.Equal("Rejected", item.MyDecision);
        // cs đã chốt Rejected, vi còn chờ — trạng thái cặp theo bản đã chốt.
        Assert.Equal(RecordingStatus.Rejected, item.RecordingStatus);

        // Bản cs đã chốt nên có đủ ba lượt; bản vi mới chỉ có lượt của tôi.
        Assert.Equal(4, item.Reviews.Count);
        Assert.Equal(new[] { 1, 2, 3, 1 }, item.Reviews.Select(r => r.Round).ToArray());
        Assert.Equal(
            [cs.RecordingId, cs.RecordingId, cs.RecordingId, vi.RecordingId],
            item.Reviews.Select(r => r.RecordingId).ToArray());
        Assert.All(item.Reviews.Take(3), r => Assert.Equal("Rejected", r.Decision));
        Assert.Equal("Approved", item.Reviews[3].Decision);

        // Lượt của tôi: không bình luận nên lý do lấy từ mô tả mã lý do.
        Assert.Equal(backgroundNoise.Description ?? backgroundNoise.ReasonCode, item.Reviews[0].Reason);
        // Có bình luận thì bình luận là lý do.
        Assert.Equal("Tiếng ồn lớn, nghe rõ tiếng quạt.", item.Reviews[1].Reason);
        // Không bình luận, không mã lý do thì để trống thay vì bịa.
        Assert.Null(item.Reviews[2].Reason);
    }

    /// <summary>
    /// Lịch sử chỉ hiện cặp mà người xem đã có phiếu, và khi đã có phiếu rồi thì được xem
    /// đủ phiếu của cả hai bên kể cả khi cặp chưa chốt. Màn hình duyệt đang hoạt động
    /// vẫn mù; lịch sử không phải chỗ để giấu phiếu của chính mình.
    /// </summary>
    [Fact]
    public async Task BanChuaChot_DaCoPhieuMinh_LoDuPhieuCuaCaHaiBen()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("mu"), RoleName.Speaker);
        var nguoiKhac = await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Người duyệt khác");

        var script = await CreateScriptAsync(
            "[vi]Nhớ [en]backup [vi]dữ liệu trước nhé.", "[vi]Nhớ sao lưu dữ liệu trước nhé.",
            ScriptDomain.ItTechnology, AdminUserId);

        var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);

        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(cs.RecordingId).WithReviewerId(ReviewerUserId).WithRound(1)
            .WithComment("Phiếu của tôi").Build());
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(cs.RecordingId).WithReviewerId(nguoiKhac.UserId).WithRound(2)
            .WithComment("Phiếu của người khác").Build());
        await Db.SaveChangesAsync(ct);

        // Hai phiếu ĐẠT là quá bán trên ba vòng, nên bản ghi đã chốt — đưa về lại chờ duyệt
        // để dựng đúng tình huống "chưa chốt mà đã có phiếu của người khác".
        await Db.Entry(cs).ReloadAsync(ct);
        cs.Status = RecordingStatus.PendingReview;
        await Db.SaveChangesAsync(ct);

        var page = await _stats.GetHistoryAsync(
            ReviewerUserId, new ReviewerHistoryQuery { PageSize = 50 }, ct);

        var item = Assert.Single(page.Items, i => i.ScriptId == script.ScriptId);

        Assert.Equal(2, item.Reviews.Count);
        Assert.Contains(item.Reviews, v => v.Reason == "Phiếu của tôi" && v.Round == 1);
        Assert.Contains(item.Reviews, v => v.Reason == "Phiếu của người khác" && v.Round == 2);
    }

    [Fact]
    public async Task BaCap_DuyetCaHaiBan_PhanTrangTheoCapVaSapXepTheoScriptId()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("hist"), RoleName.Speaker);
        var token = Guid.NewGuid().ToString("N")[..10];
        var baseTime = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

        var scriptIds = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            var script = await CreateScriptAsync(
                $"[vi]Cap [en]duyet {token} so {i}", $"[vi]Cặp duyệt {token} số {i}",
                ScriptDomain.ItTechnology, AdminUserId);

            var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
            var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);

            var csReview = new TestDataBuilders.ReviewBuilder()
                .WithRecordingId(cs.RecordingId).WithReviewerId(ReviewerUserId).WithRound(1).Build();
            var viReview = new TestDataBuilders.ReviewBuilder()
                .WithRecordingId(vi.RecordingId).WithReviewerId(ReviewerUserId).WithRound(1).Build();
            Db.Reviews.Add(csReview);
            Db.Reviews.Add(viReview);

            // Mốc duyệt tăng theo thứ tự tạo: cặp tạo sau được duyệt gần đây hơn, nên thứ tự
            // mặc định (duyệt gần nhất trước) là NGƯỢC với thứ tự mã câu.
            csReview.ReviewedAt = baseTime.AddHours(i);
            viReview.ReviewedAt = baseTime.AddHours(i).AddMinutes(30);
            await Db.SaveChangesAsync(ct);

            scriptIds.Add(script.ScriptId);
        }

        var theoMaNguoc = scriptIds.OrderByDescending(id => id, StringComparer.Ordinal).ToArray();
        var theoMaXuoi = scriptIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();

        // Mặc định: cặp được duyệt gần nhất trước. Sáu lượt duyệt gộp thành BA dòng.
        var macDinh = await _stats.GetHistoryAsync(
            ReviewerUserId, new ReviewerHistoryQuery { PageSize = 50 }, ct);

        Assert.Equal(3, macDinh.Total);
        Assert.Equal(3, macDinh.Items.Count);
        Assert.Equal(theoMaNguoc, macDinh.Items.Select(i => i.ScriptId).ToArray());

        // sort=scriptId tăng dần: dòng trả về theo mã câu, không theo mốc duyệt.
        var theoMa = await _stats.GetHistoryAsync(
            ReviewerUserId,
            new ReviewerHistoryQuery { PageSize = 50, Sort = "scriptId", Dir = "asc" },
            ct);
        Assert.Equal(theoMaXuoi, theoMa.Items.Select(i => i.ScriptId).ToArray());

        // `status` là trạng thái TÍNH RA của cặp nên không phải cột sắp xếp:
        // gõ vào thì ra đúng thứ tự mặc định.
        var sai = await _stats.GetHistoryAsync(
            ReviewerUserId,
            new ReviewerHistoryQuery { PageSize = 50, Sort = "status", Dir = "asc" },
            ct);
        Assert.Equal(macDinh.Items.Select(i => i.ScriptId), sai.Items.Select(i => i.ScriptId));
    }

    [Fact]
    public async Task LichSuDuyetCau_CoNguoiGui_Alignment_VaLyDoKhiTuChoi()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("hist"), RoleName.Speaker, fullName: "Người gửi câu");

        // Câu tôi duyệt ĐẠT, kèm cặp từ Anh-Việt để alignment có dữ liệu.
        var script = await CreateScriptAsync(
            "[vi]Tôi sẽ [en]deploy [vi]tối nay.", "[vi]Tôi sẽ triển khai tối nay.",
            ScriptDomain.ItTechnology, speaker.UserId);
        Db.ScriptWords.Add(new ScriptWord
        {
            ScriptId = script.ScriptId,
            WordPosition = 1,
            EnWord = "deploy",
            ViWord = "triển khai",
            Relation = ScriptWordRelation.SemanticEquivalent
        });
        Db.ScriptReviews.Add(new ScriptReview
        {
            ScriptId = script.ScriptId,
            UserId = ReviewerUserId,
            Action = ScriptReviewAction.Accepted,
            Comment = "Câu ổn, giữ nguyên.",
            ReviewedAt = DateTimeOffset.UtcNow
        });

        // Câu tôi duyệt TỪ CHỐI kèm lý do lỗi.
        var rejected = await CreateScriptAsync(
            "[vi]Câu này [en]sai nghĩa rồi.", "[vi]Câu này sai nghĩa rồi.",
            ScriptDomain.Education, speaker.UserId);
        var grammar = await Db.ScriptErrorReasons.FirstAsync(r => r.ReasonCode == "grammar", ct);
        Db.ScriptReviews.Add(new ScriptReview
        {
            ScriptId = rejected.ScriptId,
            UserId = ReviewerUserId,
            ErrorReasonId = grammar.ReasonId,
            Action = ScriptReviewAction.Rejected,
            Comment = "Câu thiếu tự nhiên, cần viết lại.",
            ReviewedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(ct);

        var page = await _stats.GetScriptHistoryAsync(
            ReviewerUserId, new ReviewerScriptHistoryQuery { PageSize = 50 }, ct);

        // Bộ dữ liệu mẫu của fixture cũng có lượt duyệt của reviewer test, nên chỉ soi
        // đúng hai câu mình vừa tạo; mã câu là duy nhất nên Single chắc chắn trúng.
        var contributed = page.Items.Single(i => i.ScriptId == script.ScriptId);
        Assert.Equal("contribution", contributed.Kind);
        Assert.Equal("Công nghệ thông tin", contributed.Category);
        Assert.Equal(script.CsContent, contributed.CsTranscript);
        Assert.Equal(script.ViContent, contributed.ViEquivalent);
        Assert.Equal(speaker.UserId, contributed.SpeakerId);
        Assert.Equal("Người gửi câu", contributed.SpeakerName);
        Assert.Equal("Accepted", contributed.MyAction);
        Assert.Null(contributed.Reason);
        Assert.Equal("Câu ổn, giữ nguyên.", contributed.Comment);
        Assert.Equal("PendingValidation", contributed.ScriptStatus);

        var align = Assert.Single(contributed.Alignment);
        Assert.Equal("deploy", align.Source);
        Assert.Equal("en", align.SourceLang);
        Assert.Equal("triển khai", align.Target);
        Assert.Equal("vi", align.TargetLang);
        Assert.Equal("semantic_equivalent", align.Relation);

        var reported = page.Items.Single(i => i.ScriptId == rejected.ScriptId);
        Assert.Equal("report", reported.Kind);
        Assert.Equal("Rejected", reported.MyAction);
        Assert.Equal(grammar.Description ?? grammar.ReasonCode, reported.Reason);
        Assert.Equal("Câu thiếu tự nhiên, cần viết lại.", reported.Comment);
        Assert.Empty(reported.Alignment);
    }

    /// <summary>
    /// Cặp có bản cs ĐÃ đạt mà bản vi chưa ai duyệt: trạng thái gộp của cặp vẫn là "đang chờ"
    /// (cặp chỉ xong khi cả hai bản xong), nhưng hai trường riêng phải nói đúng bản nào đã
    /// xong. Thiếu chúng thì màn hình chỉ ghi "Chờ duyệt" và giấu mất một nửa đã được duyệt.
    /// </summary>
    [Fact]
    public async Task CapNuaChung_TrangThaiTungBanNoiDungBanNaoDaXong()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("nua"), RoleName.Speaker);
        var nguoiKhac = await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Người duyệt thứ hai");

        var script = await CreateScriptAsync(
            "[vi]Tối nay bên kỹ thuật sẽ [en]update [vi]máy chủ.",
            "[vi]Tối nay bên kỹ thuật sẽ cập nhật máy chủ.",
            ScriptDomain.ItTechnology, AdminUserId);

        var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);

        // Hai phiếu đạt trên bản cs là quá bán — trigger chốt bản đó ngay. Bản vi không ai duyệt.
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(cs.RecordingId).WithReviewerId(ReviewerUserId).WithRound(1).Build());
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(cs.RecordingId).WithReviewerId(nguoiKhac.UserId).WithRound(2).Build());
        await Db.SaveChangesAsync(ct);

        await Db.Entry(cs).ReloadAsync(ct);
        Assert.Equal(RecordingStatus.Approved, cs.Status);

        // --- lịch sử của Reviewer
        var cuaReviewer = await _stats.GetHistoryAsync(
            ReviewerUserId, new ReviewerHistoryQuery { PageSize = 50 }, ct);

        var dongRv = Assert.Single(cuaReviewer.Items, i => i.ScriptId == script.ScriptId);
        Assert.Equal(RecordingStatus.PendingReview, dongRv.RecordingStatus);
        Assert.Equal(RecordingStatus.Approved, dongRv.CsStatus);
        Assert.Equal(RecordingStatus.PendingReview, dongRv.ViStatus);

        // --- lịch sử của Speaker
        var rounds = Services.GetRequiredService<ISpeakerRoundsService>();
        var cuaSpeaker = await rounds.GetRecordingHistoryAsync(
            speaker.UserId, new SpeakerHistoryQuery { PageSize = 50 }, ct);

        var dongSp = Assert.Single(cuaSpeaker.Items, i => i.ScriptId == script.ScriptId);
        Assert.Equal(RecordingStatus.PendingReview, dongSp.Status);
        Assert.Equal(RecordingStatus.Approved, dongSp.CsStatus);
        Assert.Equal(RecordingStatus.PendingReview, dongSp.ViStatus);
        Assert.Equal(vi.RecordingId, dongSp.ViRecordingId);
    }
}
