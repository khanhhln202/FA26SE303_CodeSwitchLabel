using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Reviews;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Luật chốt theo đa số nằm ở trigger trg_review_majority dưới database. Các test này khoá
/// bản sao trong C# đúng bằng luật đó — lệch nhau là API báo một đằng, database ghi một nẻo.
/// </summary>
public class ReviewRulesTests
{
    private const ReviewDecision Ok = ReviewDecision.Approved;
    private const ReviewDecision No = ReviewDecision.Rejected;

    private const int Required = ReviewRules.RoundsRequiredInDatabase;

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public void VongKeTiep_LaSoLuotDaCoCongMot(int existing, int expected)
    {
        Assert.Equal(expected, ReviewRules.NextRound(existing));
    }

    [Fact]
    public void ChuaDuLuot_ChuaChot()
    {
        Assert.Null(ReviewRules.Outcome([Ok], Required));
        Assert.Null(ReviewRules.Outcome([Ok, No], Required));
    }

    [Theory]
    [InlineData(Ok, Ok, Ok)]
    [InlineData(Ok, Ok, No)]
    [InlineData(No, Ok, Ok)]
    public void DuBaLuot_HaiPhieuDat_ThiDat(ReviewDecision a, ReviewDecision b, ReviewDecision c)
    {
        Assert.Equal(RecordingStatus.Approved, ReviewRules.Outcome([a, b, c], Required));
    }

    [Theory]
    [InlineData(No, No, No)]
    [InlineData(Ok, No, No)]
    [InlineData(No, Ok, No)]
    public void DuBaLuot_HaiPhieuTuChoi_ThiTuChoi(ReviewDecision a, ReviewDecision b, ReviewDecision c)
    {
        Assert.Equal(RecordingStatus.Rejected, ReviewRules.Outcome([a, b, c], Required));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void DuLuotHayChua(int reviewCount, bool expected)
    {
        Assert.Equal(expected, ReviewRules.IsComplete(reviewCount, Required));
    }

    [Fact]
    public void SoVongPhaiLonHonKhong()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReviewRules.Outcome([Ok], 0));
    }

    // ----------------------------------------------- vì sao chưa duyệt được

    [Fact]
    public void ConChoDuyetVaChuaAiCham_ThiDuyetDuoc()
    {
        var blocker = ReviewRules.BlockerFor(
            isOwnRecording: false, reviewedByMe: false,
            RecordingStatus.PendingReview, reviewCount: 0, Required);

        Assert.Equal(ReviewBlocker.None, blocker);
    }

    [Fact]
    public void BanGhiCuaChinhMinh_ChanTruoc()
    {
        var blocker = ReviewRules.BlockerFor(
            isOwnRecording: true, reviewedByMe: false,
            RecordingStatus.PendingReview, reviewCount: 0, Required);

        Assert.Equal(ReviewBlocker.OwnRecording, blocker);
    }

    [Theory]
    [InlineData(RecordingStatus.PendingReview)]
    [InlineData(RecordingStatus.Approved)]
    [InlineData(RecordingStatus.Rejected)]
    public void DaDuyetRoi_ThiBaoDaDuyet_KeCaKhiBanGhiDaChot(RecordingStatus status)
    {
        // Màn hình cần đánh dấu "mình làm rồi" ngay cả với bản ghi đã chốt xong,
        // nên lý do này phải thắng lý do "không còn chờ duyệt".
        var blocker = ReviewRules.BlockerFor(
            isOwnRecording: false, reviewedByMe: true, status, reviewCount: 3, Required);

        Assert.Equal(ReviewBlocker.AlreadyReviewedByMe, blocker);
    }

    [Theory]
    [InlineData(RecordingStatus.Approved)]
    [InlineData(RecordingStatus.Rejected)]
    [InlineData(RecordingStatus.QcFailed)]
    public void BanGhiKhongConChoDuyet(RecordingStatus status)
    {
        var blocker = ReviewRules.BlockerFor(
            isOwnRecording: false, reviewedByMe: false, status, reviewCount: 3, Required);

        Assert.Equal(ReviewBlocker.NotPendingReview, blocker);
    }

    [Fact]
    public void DuLuotNhungChuaKipChot_ThiHetCho()
    {
        // Trigger chốt ngay trong transaction, nhưng vẫn phải có nhánh này: nếu số vòng cấu hình
        // nhỏ hơn 3 thì bản ghi đủ lượt mà trạng thái chưa đổi.
        var blocker = ReviewRules.BlockerFor(
            isOwnRecording: false, reviewedByMe: false,
            RecordingStatus.PendingReview, reviewCount: 3, Required);

        Assert.Equal(ReviewBlocker.RoundsFull, blocker);
    }
}
