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
/// Admin xem bản ghi theo CẶP, và trạng thái cặp phải khớp lịch sử Reviewer/Speaker:
/// một bản đạt mà bản kia còn chờ thì cặp vẫn chờ, không được báo "đã duyệt".
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class AdminRecordingPairTests : IntegrationTestBase
{
    private readonly IAdminRecordingService _pairs;

    public AdminRecordingPairTests(DatabaseFixture fixture) : base(fixture)
    {
        _pairs = Services.GetRequiredService<IAdminRecordingService>();
    }

    private async Task<AppUser> ApprovedReviewerAsync()
        => await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Người duyệt thứ hai");

    private async Task AddReviewsAsync(
        Recording recording, long otherReviewerId, ReviewDecision decision, CancellationToken ct)
    {
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(recording.RecordingId).WithReviewerId(ReviewerUserId).WithRound(1)
            .WithDecision(decision).Build());
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(recording.RecordingId).WithReviewerId(otherReviewerId).WithRound(2)
            .WithDecision(decision).Build());
        await Db.SaveChangesAsync(ct);
        await Db.Entry(recording).ReloadAsync(ct);
    }

    private Task<Script> CreatePairScriptAsync()
        => CreateScriptAsync(
            "[vi]Mai [en]deploy [vi]bản mới nhé.", "[vi]Mai triển khai bản mới nhé.",
            ScriptDomain.ItTechnology, AdminUserId);

    /// <summary>
    /// Cặp của đúng bài test này. Cả bộ test dùng chung database nên không được
    /// Assert.Single trên cả trang — sẽ vấp dữ liệu của test khác.
    /// </summary>
    private async Task<AdminRecordingPairDto> PairAsync(
        string scriptId, long speakerId, CancellationToken ct)
    {
        for (var pageNo = 1; ; pageNo++)
        {
            var page = await _pairs.GetPairsAsync(new PageRequest { Page = pageNo, PageSize = 100 }, ct);
            var hit = page.Items.FirstOrDefault(p => p.ScriptId == scriptId && p.SpeakerId == speakerId);
            if (hit is not null) return hit;
            Assert.True(pageNo < page.TotalPages, $"Không thấy cặp {scriptId}/{speakerId} trong danh sách.");
        }
    }

    [Fact]
    public async Task MotBanDat_MotBanCho_CapVanCho()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("pair"), RoleName.Speaker, "Người đọc cặp");
        var script = await CreatePairScriptAsync();
        var other = await ApprovedReviewerAsync();

        var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);
        await AddReviewsAsync(cs, other.UserId, ReviewDecision.Approved, ct);

        var pair = await PairAsync(script.ScriptId, speaker.UserId, ct);

        Assert.Equal(RecordingStatus.PendingReview, pair.Status);
        Assert.Equal(RecordingStatus.Approved, pair.CsStatus);
        Assert.Equal(RecordingStatus.PendingReview, pair.ViStatus);
        Assert.Equal(cs.RecordingId, pair.CsRecordingId);
        Assert.Equal(vi.RecordingId, pair.ViRecordingId);
        Assert.Equal(2, pair.Reviews.Count);
        Assert.All(pair.Reviews, r => Assert.False(string.IsNullOrWhiteSpace(r.ReviewerName)));
        Assert.All(pair.Reviews, r => Assert.Equal(cs.RecordingId, r.RecordingId));
    }

    [Fact]
    public async Task CaHaiBanDat_CapDat()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("pair"), RoleName.Speaker);
        var script = await CreatePairScriptAsync();
        var other = await ApprovedReviewerAsync();

        var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);
        await AddReviewsAsync(cs, other.UserId, ReviewDecision.Approved, ct);
        await AddReviewsAsync(vi, other.UserId, ReviewDecision.Approved, ct);

        var pair = await PairAsync(script.ScriptId, speaker.UserId, ct);

        Assert.Equal(RecordingStatus.Approved, pair.Status);
        Assert.Equal(RecordingStatus.Approved, pair.CsStatus);
        Assert.Equal(RecordingStatus.Approved, pair.ViStatus);
    }

    [Fact]
    public async Task MotBanBiTuChoi_CapTuChoi()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("pair"), RoleName.Speaker);
        var script = await CreatePairScriptAsync();
        var other = await ApprovedReviewerAsync();

        var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);
        await AddReviewsAsync(cs, other.UserId, ReviewDecision.Rejected, ct);
        await AddReviewsAsync(vi, other.UserId, ReviewDecision.Approved, ct);

        var pair = await PairAsync(script.ScriptId, speaker.UserId, ct);

        Assert.Equal(RecordingStatus.Rejected, pair.Status);
        Assert.Equal(RecordingStatus.Rejected, pair.CsStatus);
        Assert.Equal(RecordingStatus.Approved, pair.ViStatus);
    }

    [Fact]
    public async Task TakeMoiNhatQuyetDinh_BanCuBiTuChoiKhongKeoCapTheo()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("pair"), RoleName.Speaker);
        var script = await CreatePairScriptAsync();
        var other = await ApprovedReviewerAsync();

        var csOld = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        await AddReviewsAsync(csOld, other.UserId, ReviewDecision.Rejected, ct);

        // Chỉ thu lại được khi bản cũ đã bị từ chối (index chỉ cho một bản chờ/đạt mỗi biến thể).
        var csNew = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);

        csOld.RecordedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        csNew.RecordedAt = DateTimeOffset.UtcNow;
        await Db.SaveChangesAsync(ct);

        await AddReviewsAsync(csNew, other.UserId, ReviewDecision.Approved, ct);
        await AddReviewsAsync(vi, other.UserId, ReviewDecision.Approved, ct);

        var pair = await PairAsync(script.ScriptId, speaker.UserId, ct);

        Assert.Equal(RecordingStatus.Approved, pair.Status);
        Assert.Equal(csNew.RecordingId, pair.CsRecordingId);
        Assert.DoesNotContain(pair.Reviews, r => r.RecordingId == csOld.RecordingId);
    }

    [Fact]
    public async Task TongThoiLuong_ChiTinhHaiTakeMoiNhat()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("pair"), RoleName.Speaker);
        var script = await CreatePairScriptAsync();
        var other = await ApprovedReviewerAsync();

        var csOld = await CreateRecordingAsync(
            script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching, durationSec: 5.0m);
        await AddReviewsAsync(csOld, other.UserId, ReviewDecision.Rejected, ct);

        var csNew = await CreateRecordingAsync(
            script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching, durationSec: 3.0m);
        var vi = await CreateRecordingAsync(
            script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese, durationSec: 4.0m);

        csOld.RecordedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        csNew.RecordedAt = DateTimeOffset.UtcNow;
        await Db.SaveChangesAsync(ct);

        var pair = await PairAsync(script.ScriptId, speaker.UserId, ct);

        Assert.Equal(csNew.RecordingId, pair.CsRecordingId);
        Assert.Equal(7.0m, pair.TotalDurationSec);
    }

    [Fact]
    public async Task CaHaiBanTruotQc_CapBaoLoiChatLuong()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserAsync(NewUniqueEmail("pair"), RoleName.Speaker);
        var script = await CreatePairScriptAsync();

        var cs = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        var vi = await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);
        cs.Status = RecordingStatus.QcFailed;
        vi.Status = RecordingStatus.QcFailed;
        await Db.SaveChangesAsync(ct);

        var pair = await PairAsync(script.ScriptId, speaker.UserId, ct);

        Assert.Equal(RecordingStatus.QcFailed, pair.Status);
        Assert.Equal(RecordingStatus.QcFailed, pair.CsStatus);
        Assert.Equal(RecordingStatus.QcFailed, pair.ViStatus);
    }
}
