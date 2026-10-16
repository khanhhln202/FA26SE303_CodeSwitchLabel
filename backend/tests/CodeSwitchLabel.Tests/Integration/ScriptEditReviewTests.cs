using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Sửa nội dung câu (action "Edited") là đường duy nhất vừa ghi lượt duyệt, vừa sửa câu, vừa chốt
/// trạng thái — nên dễ hỏng nhất. Trước đây cả bộ test không có mục nào chạm nhánh này.
///
/// Hai lỗi từng xảy ra ở đây, cùng một nguyên nhân (lưu hai lần thay vì một):
///   1. Lần lưu thứ nhất đẩy updated_at lên SAU reviewed_at, nên trigger
///      trg_script_validated_domain không công nhận lượt duyệt vừa ghi → 422 cho mọi bản sửa.
///   2. Lần lưu thứ nhất đã commit xong, lần thứ hai mới ném lỗi: API báo thất bại mà nội dung
///      câu đã bị ghi đè và lượt duyệt đã nằm trong bảng. Người duyệt thử lại thì 409.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ScriptEditReviewTests : IntegrationTestBase
{
    public ScriptEditReviewTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    /// <summary>Sửa nội dung thì phải ra kết quả, không phải 422.</summary>
    [Fact]
    public async Task SuaNoiDung_ChoScriptValidatedVaGhiNoiDungMoi()
    {
        var script = await CreateScriptAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.",
            "[vi]Em nên quét tài liệu này rồi gửi qua thư điện tử cho tôi.",
            ScriptDomain.ItTechnology,
            SpeakerUserId,
            enWordCount: 2);

        var ketQua = await ScriptService.ReviewAsync(script.ScriptId, ReviewerUserId, new ReviewScriptRequest
        {
            Action = ScriptReviewAction.Edited,
            EditedCsContent = "[vi]Em nên [en]scan [vi]tài liệu mới rồi gửi qua [en]email [vi]cho tôi.",
            EditedViContent = "[vi]Em nên quét tài liệu mới rồi gửi qua thư điện tử cho tôi.",
            Comment = "Thêm 'mới' cho rõ"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(ScriptStatus.Validated, ketQua.Status);
        Assert.Contains("tài liệu mới", ketQua.CsContent, StringComparison.Ordinal);

        var luu = await Db.Scripts.AsNoTracking()
            .SingleAsync(s => s.ScriptId == script.ScriptId, TestContext.Current.CancellationToken);
        Assert.Equal(ScriptStatus.Validated, luu.Status);
        Assert.Contains("tài liệu mới", luu.CsContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lượt duyệt bị từ chối thì không được để lại gì: không lượt duyệt, không đổi nội dung.
    /// Đây là lỗi số 2 ở trên — trước đây nội dung bị ghi đè dù API trả lỗi.
    /// </summary>
    [Fact]
    public async Task SuaNoiDungBiTuChoi_KhongDongLaiLượtDuyetVaKhongDoiNoiDung()
    {
        var script = await CreateScriptAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.",
            "[vi]Em nên quét tài liệu này rồi gửi qua thư điện tử cho tôi.",
            ScriptDomain.ItTechnology,
            SpeakerUserId,
            enWordCount: 2);

        var truoc = await Db.Scripts.AsNoTracking()
            .SingleAsync(s => s.ScriptId == script.ScriptId, TestContext.Current.CancellationToken);

        // 1 từ tiếng Anh trong khi câu gốc có 2 → bị chặn bởi en_word_count_locked.
        var loi = await Assert.ThrowsAsync<CodeSwitchLabel.Services.Common.UnprocessableException>(
            () => ScriptService.ReviewAsync(script.ScriptId, ReviewerUserId, new ReviewScriptRequest
            {
                Action = ScriptReviewAction.Edited,
                EditedCsContent = "[vi]Em nên [en]scan [vi]tài liệu này nhé.",
                EditedViContent = "[vi]Em nên quét tài liệu này nhé.",
                Comment = "Cố tình sai số từ tiếng Anh"
            }, TestContext.Current.CancellationToken));

        Assert.Equal("en_word_count_locked", loi.Code);

        Assert.Empty(await Db.ScriptReviews
            .Where(r => r.ScriptId == script.ScriptId)
            .ToListAsync(TestContext.Current.CancellationToken));

        var sau = await Db.Scripts.AsNoTracking()
            .SingleAsync(s => s.ScriptId == script.ScriptId, TestContext.Current.CancellationToken);
        Assert.Equal(truoc.CsContent, sau.CsContent);
        Assert.Equal(truoc.Status, sau.Status);
    }
}