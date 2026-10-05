using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Reviews;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Luật chốt theo đa số nằm ở trigger trg_review_majority dưới database. Các test này khoá
/// bản sao trong C# đúng bằng luật đó — lệch nhau là API báo một đằng, database ghi một nẻo.
/// </summary>
[Trait("Category", "Unit")]
public class ReviewRulesTests
{
    private const ReviewDecision Ok = ReviewDecision.Approved;
    private const ReviewDecision No = ReviewDecision.Rejected;

    // Ghim cứng con số trigger đang chờ. Không dùng RoundsRequiredInDatabase trực tiếp:
    // dùng hằng của code thì đổi hằng là test tự xanh mà database vẫn chờ số cũ.
    private const int Required = 3;

    [Fact]
    public void SoVongDatabase_DangChoDungBaLuot()
    {
        // Arrange + Act + Assert — lệch là báo động: phải sửa trigger hoặc code cùng lúc.
        Assert.Equal(3, ReviewRules.RoundsRequiredInDatabase);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public void VongKeTiep_LaSoLuotDaCoCongMot(int existing, int expected)
    {
        // Arrange + Act
        var next = ReviewRules.NextRound(existing);

        // Assert
        Assert.Equal(expected, next);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void VongKeTiep_VoiSoAm_VanCongMot(int existing)
    {
        // Arrange + Act
        var next = ReviewRules.NextRound(existing);

        // Assert
        Assert.Equal(existing + 1, next);
    }

    [Fact]
    public void ChuaDuLuot_ChuaChot()
    {
        // Arrange + Act + Assert
        Assert.Null(ReviewRules.Outcome([Ok], Required));
        Assert.Null(ReviewRules.Outcome([Ok, No], Required));
    }

    [Theory]
    [InlineData(Ok, Ok, Ok)]
    [InlineData(Ok, Ok, No)]
    [InlineData(No, Ok, Ok)]
    public void DuBaLuot_HaiPhieuDat_ThiDat(ReviewDecision a, ReviewDecision b, ReviewDecision c)
    {
        // Arrange + Act
        var outcome = ReviewRules.Outcome([a, b, c], Required);

        // Assert
        Assert.Equal(RecordingStatus.Approved, outcome);
    }

    [Theory]
    [InlineData(No, No, No)]
    [InlineData(Ok, No, No)]
    [InlineData(No, Ok, No)]
    public void DuBaLuot_HaiPhieuTuChoi_ThiTuChoi(ReviewDecision a, ReviewDecision b, ReviewDecision c)
    {
        // Arrange + Act
        var outcome = ReviewRules.Outcome([a, b, c], Required);

        // Assert
        Assert.Equal(RecordingStatus.Rejected, outcome);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void DuLuotHayChua(int reviewCount, bool expected)
    {
        // Arrange + Act
        var complete = ReviewRules.IsComplete(reviewCount, Required);

        // Assert
        Assert.Equal(expected, complete);
    }

    [Fact]
    public void SoVongPhaiLonHonKhong()
    {
        // Arrange + Act
        void Act() => ReviewRules.Outcome([Ok], 0);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(Act);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(4)]
    public void Outcome_VoiSoLuotCauHinhLa_TheoDungNguongDo(int roundsRequired)
    {
        // Arrange — đủ lượt theo đúng cấu hình truyền vào thì chốt, thiếu thì chờ.
        var decisions = new[] { Ok, Ok, Ok };

        // Act
        var outcome = ReviewRules.Outcome(decisions, roundsRequired);

        // Assert
        if (roundsRequired <= decisions.Length)
            Assert.NotNull(outcome);
        else
            Assert.Null(outcome);
    }

    // ----------------------------------------------- vì sao chưa duyệt được

    [Fact]
    public void ConChoDuyetVaChuaAiCham_ThiDuyetDuoc()
    {
        // Arrange + Act
        var blocker = ReviewRules.BlockerFor(
            isOwnRecording: false, reviewedByMe: false,
            status: RecordingStatus.PendingReview, reviewCount: 0, roundsRequired: Required);

        // Assert
        Assert.Equal(ReviewBlocker.None, blocker);
    }

    [Fact]
    public void BanGhiCuaChinhMinh_ChanTruoc()
    {
        // Arrange + Act
        var blocker = ReviewRules.BlockerFor(
            isOwnRecording: true, reviewedByMe: false,
            status: RecordingStatus.PendingReview, reviewCount: 0, roundsRequired: Required);

        // Assert
        Assert.Equal(ReviewBlocker.OwnRecording, blocker);
    }

    [Theory]
    [InlineData(RecordingStatus.PendingReview)]
    [InlineData(RecordingStatus.Approved)]
    [InlineData(RecordingStatus.Rejected)]
    public void DaDuyetRoi_ThiBaoDaDuyet_KeCaKhiBanGhiDaChot(RecordingStatus status)
    {
        // Arrange — màn hình cần đánh dấu "mình làm rồi" ngay cả với bản ghi đã chốt xong,
        // nên lý do này phải thắng lý do "không còn chờ duyệt".
        // Act
        var blocker = ReviewRules.BlockerFor(
            isOwnRecording: false, reviewedByMe: true, status: status, reviewCount: 3, roundsRequired: Required);

        // Assert
        Assert.Equal(ReviewBlocker.AlreadyReviewedByMe, blocker);
    }

    [Theory]
    [InlineData(RecordingStatus.Approved)]
    [InlineData(RecordingStatus.Rejected)]
    [InlineData(RecordingStatus.QcFailed)]
    public void BanGhiKhongConChoDuyet(RecordingStatus status)
    {
        // Arrange + Act
        var blocker = ReviewRules.BlockerFor(
            isOwnRecording: false, reviewedByMe: false, status: status, reviewCount: 3, roundsRequired: Required);

        // Assert
        Assert.Equal(ReviewBlocker.NotPendingReview, blocker);
    }

    [Fact]
    public void DuLuotNhungChuaKipChot_ThiHetCho()
    {
        // Arrange — trigger chốt ngay trong transaction, nhưng vẫn phải có nhánh này: nếu số vòng cấu hình
        // nhỏ hơn 3 thì bản ghi đủ lượt mà trạng thái chưa đổi.
        // Act
        var blocker = ReviewRules.BlockerFor(
            isOwnRecording: false, reviewedByMe: false,
            status: RecordingStatus.PendingReview, reviewCount: 3, roundsRequired: Required);

        // Assert
        Assert.Equal(ReviewBlocker.RoundsFull, blocker);
    }
}
