using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// SR-16 (báo cáo tự rà soát 24/10/2026): câu đã duyệt rồi bị đa số từ chối bản ghi vì lý do nội dung
/// thì quay về chờ duyệt (fn_review_majority_status) — và phải duyệt lại được.
///
/// Trước đây chốt "mỗi câu chỉ một lượt" đếm MỌI lượt Accepted/Edited từng có, nên câu quay về
/// chờ duyệt bị 409 text_review_already_done ở mọi lần duyệt, trong khi trigger
/// fn_script_validated_by_domain_reviewer lại đòi một lượt duyệt MỚI sau lần đặt lại mới cho
/// validated. Kết quả: vòng WF03 → WF01 đứt, câu kẹt ở chờ duyệt vĩnh viễn.
///
/// Lưu ý về bộ test: mỗi test chạy trong MỘT transaction nên now() của database đứng yên ở
/// lúc mở transaction và không có lần "đặt lại" nào đi qua mốc thời gian được. Vì vậy lượt duyệt của
/// vòng trước được dựng bằng cách đóng dấu nó TRƯỚC script.updated_at, đúng quan hệ thời gian
/// mà production có sau khi câu bị trả về.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ScriptReReviewTests : IntegrationTestBase
{
    public ScriptReReviewTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    private Task<Script> CreatePendingScriptAsync()
    {
        var token = Guid.NewGuid().ToString("N")[..8];

        return CreateScriptAsync(
            $"[vi]Em nên [en]scan [vi]tài liệu mã {token} này.",
            $"[vi]Em nên quét tài liệu mã {token} này.",
            ScriptDomain.ItTechnology,
            SpeakerUserId);
    }

    private async Task AddAcceptedReviewAsync(string scriptId, DateTimeOffset reviewedAt)
    {
        Db.ScriptReviews.Add(new ScriptReview
        {
            ScriptId = scriptId,
            UserId = ReviewerUserId,
            Action = ScriptReviewAction.Accepted,
            ReviewedAt = reviewedAt
        });

        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ReviewScriptRequest Accept() => new() { Action = ScriptReviewAction.Accepted };

    // ------------------------------------------------------------ repository

    [Fact]
    public async Task HasReviewInCurrentRound_False_KhiChuaCoLuotDuyetNao()
    {
        var script = await CreatePendingScriptAsync();

        Assert.False(await Scripts.HasReviewInCurrentRoundAsync(script.ScriptId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HasReviewInCurrentRound_False_KhiLuotDuyetCoTruocMocDatLai()
    {
        var script = await CreatePendingScriptAsync();
        await AddAcceptedReviewAsync(script.ScriptId, script.UpdatedAt.AddHours(-1));

        Assert.False(await Scripts.HasReviewInCurrentRoundAsync(script.ScriptId, TestContext.Current.CancellationToken));

        // Hàm cũ vẫn trả "từng có" — chính sự khác nhau này là SR-16.
        Assert.True(await Scripts.HasAcceptedReviewAsync(script.ScriptId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HasReviewInCurrentRound_True_KhiLuotDuyetTuMocDatLaiTroDi()
    {
        var script = await CreatePendingScriptAsync();
        await AddAcceptedReviewAsync(script.ScriptId, script.UpdatedAt.AddSeconds(5));

        Assert.True(await Scripts.HasReviewInCurrentRoundAsync(script.ScriptId, TestContext.Current.CancellationToken));
    }

    // --------------------------------------------------------------- service

    /// <summary>Lỗi SR-16: trước khi sửa, test này nhận 409 text_review_already_done.</summary>
    [Fact]
    public async Task CauQuayVeChoDuyet_DuyetLaiDuocVaChotLaiValidated()
    {
        var script = await CreatePendingScriptAsync();
        await AddAcceptedReviewAsync(script.ScriptId, script.UpdatedAt.AddHours(-1));

        var ketQua = await ScriptService.ReviewAsync(
            script.ScriptId, ReviewerUserId, Accept(), TestContext.Current.CancellationToken);

        Assert.Equal(ScriptStatus.Validated, ketQua.Status);

        var soLuot = await Db.ScriptReviews.AsNoTracking()
            .CountAsync(r => r.ScriptId == script.ScriptId, TestContext.Current.CancellationToken);
        Assert.Equal(2, soLuot);
    }

    /// <summary>Chốt "mỗi vòng một lượt" vẫn còn: câu đã Validated không duyệt lần hai được.</summary>
    [Fact]
    public async Task CauDaValidated_LuotDuyetThuHai_BiChan()
    {
        var script = await CreatePendingScriptAsync();

        await ScriptService.ReviewAsync(
            script.ScriptId, ReviewerUserId, Accept(), TestContext.Current.CancellationToken);

        var loi = await Assert.ThrowsAsync<ConflictException>(
            () => ScriptService.ReviewAsync(
                script.ScriptId, ReviewerUserId, Accept(), TestContext.Current.CancellationToken));

        Assert.Equal("text_review_already_done", loi.Code);
    }

    /// <summary>
    /// Chốt cho khoảng hở mili-giây vẫn còn: lượt duyệt của VÒNG NÀY đã nằm trong database
    /// nhưng câu chưa kịp chốt Validated thì người thứ hai bị chặn, không ghi đè.
    /// </summary>
    [Fact]
    public async Task LuotDuyetCuaVongNayDangDo_ChanNguoiThuHai()
    {
        var script = await CreatePendingScriptAsync();
        await AddAcceptedReviewAsync(script.ScriptId, script.UpdatedAt.AddSeconds(5));

        var loi = await Assert.ThrowsAsync<ConflictException>(
            () => ScriptService.ReviewAsync(
                script.ScriptId, ReviewerUserId, Accept(), TestContext.Current.CancellationToken));

        Assert.Equal("text_review_already_done", loi.Code);

        var soLuot = await Db.ScriptReviews.AsNoTracking()
            .CountAsync(r => r.ScriptId == script.ScriptId, TestContext.Current.CancellationToken);
        Assert.Equal(1, soLuot);
    }
}
