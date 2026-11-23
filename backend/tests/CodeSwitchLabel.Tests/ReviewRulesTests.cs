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

    // Ghim cứng con số, không dùng hằng của code: dùng hằng thì đổi hằng là test tự xanh
    // mà database vẫn chờ số cũ. Tham số `review.rounds_required` đổi được, nên 3 ở đây là
    // luật của các test này chứ không phải luật của hệ thống.
    private const int Required = 3;

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
    public void ChuaBenNaoQuaBan_ChuaChot()
    {
        // Arrange + Act + Assert
        Assert.Null(ReviewRules.Outcome([Ok], Required));
        Assert.Null(ReviewRules.Outcome([Ok, No], Required));
    }

    /// <summary>
    /// Quá bán là chốt, không chờ cho đủ lượt. Hai phiếu giống nhau trên ba vòng thì lượt
    /// thứ ba không lật được kết quả, mà để bản ghi nằm "chờ duyệt" thì người đọc không
    /// biết đường thu lại.
    /// </summary>
    [Theory]
    [InlineData(Ok, Ok, RecordingStatus.Approved)]
    [InlineData(No, No, RecordingStatus.Rejected)]
    public void QuaBanSom_ChotNgayKhiChuaDuLuot(
        ReviewDecision a, ReviewDecision b, RecordingStatus mongDoi)
    {
        // Arrange + Act
        var outcome = ReviewRules.Outcome([a, b], Required);

        // Assert
        Assert.Equal(mongDoi, outcome);
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

    /// <summary>
    /// Ngưỡng quá bán đi theo cấu hình: cần 4 hoặc 5 lượt thì ba phiếu đạt đã là quá bán,
    /// cần 7 lượt thì chưa.
    /// </summary>
    [Theory]
    [InlineData(4, RecordingStatus.Approved)]
    [InlineData(5, RecordingStatus.Approved)]
    [InlineData(7, null)]
    public void Outcome_NguongQuaBan_DiTheoSoVongCauHinh(int roundsRequired, RecordingStatus? mongDoi)
    {
        // Arrange
        var decisions = new[] { Ok, Ok, Ok };

        // Act
        var outcome = ReviewRules.Outcome(decisions, roundsRequired);

        // Assert
        Assert.Equal(mongDoi, outcome);
    }

    /// <summary>Số vòng chẵn mà hoà thì trượt — không đủ phiếu đạt nghĩa là không đạt.</summary>
    [Fact]
    public void SoVongChan_HoaPhieu_ThiTruot()
    {
        // Arrange + Act
        var outcome = ReviewRules.Outcome([Ok, Ok, No, No], 4);

        // Assert
        Assert.Equal(RecordingStatus.Rejected, outcome);
    }

    // ----------------------------------------------- trạng thái của CẶP

    [Theory]
    [InlineData(RecordingStatus.Approved, RecordingStatus.Approved, RecordingStatus.Approved)]
    [InlineData(RecordingStatus.Approved, RecordingStatus.PendingReview, RecordingStatus.PendingReview)]
    [InlineData(RecordingStatus.Approved, null, RecordingStatus.PendingReview)]
    [InlineData(RecordingStatus.Rejected, null, RecordingStatus.Rejected)]
    [InlineData(RecordingStatus.Approved, RecordingStatus.Rejected, RecordingStatus.Rejected)]
    [InlineData(null, RecordingStatus.Rejected, RecordingStatus.Rejected)]
    [InlineData(RecordingStatus.QcFailed, RecordingStatus.QcFailed, RecordingStatus.QcFailed)]
    [InlineData(RecordingStatus.QcFailed, null, RecordingStatus.QcFailed)]
    [InlineData(null, RecordingStatus.QcFailed, RecordingStatus.QcFailed)]
    [InlineData(RecordingStatus.QcFailed, RecordingStatus.PendingReview, RecordingStatus.PendingReview)]
    [InlineData(RecordingStatus.QcFailed, RecordingStatus.Approved, RecordingStatus.PendingReview)]
    [InlineData(null, null, RecordingStatus.PendingReview)]
    public void TrangThaiCap_TheoHaiBan(
        RecordingStatus? cs, RecordingStatus? vi, RecordingStatus mongDoi)
    {
        // Arrange + Act
        var pair = ReviewRules.PairOutcome(cs, vi);

        // Assert
        Assert.Equal(mongDoi, pair);
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

    // ----------------------------------------------------------------- giấu thế hoà
    //
    // Thế hoà 1–1 là việc của máy duyệt, nên giấu khỏi danh sách của người. Thứ đáng test
    // nhất ở đây KHÔNG phải "có giấu được không" mà là "KHÔNG giấu khi không có ai nhặt":
    // giấu sai lúc đó tạo ra bản ghi người không thấy mà máy cũng không lấy.

    private static readonly DateTimeOffset BayGio = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan NhipQuet = TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ThieuMotCongTac_ThiKHONG_GiauBanNao(bool mayDuyetBat, bool tuDongBat)
    {
        var moc = ReviewRules.HideTiesNewerThan(
            mayDuyetBat, tuDongBat, NhipQuet, maxAttempts: 3, BayGio);

        Assert.Null(moc);
    }

    [Fact]
    public void BatCaHaiCongTac_ThiGiauTheHoaConTuoi()
    {
        var moc = ReviewRules.HideTiesNewerThan(
            aiReviewEnabled: true, autoEnabled: true, NhipQuet, maxAttempts: 3, BayGio);

        // 60 giây × (3 lần thử + 1 nhịp dự phòng) = 4 phút.
        Assert.Equal(BayGio.AddMinutes(-4), moc);
    }

    [Fact]
    public void AnHan_DaiRa_KhiNhipQuetDaiRa()
    {
        // Ai nới nhịp quét thì ân hạn phải nới theo, nếu không thế hoà hiện lại cho người
        // đúng lúc máy vẫn đang xếp hàng xử lý nó.
        var moc = ReviewRules.HideTiesNewerThan(
            aiReviewEnabled: true, autoEnabled: true,
            TimeSpan.FromSeconds(300), maxAttempts: 3, BayGio);

        Assert.Equal(BayGio.AddMinutes(-20), moc);
    }

    [Fact]
    public void TheHoaCu_Hon_AnHan_Thi_HIEN_LAI_ChoNguoi()
    {
        // Đây là cái van an toàn. Máy tắt giữa chừng, nhà cung cấp chết, hay máy bỏ cuộc sau
        // ba lần thử — kiểu gì thì thế hoà nằm quá lâu cũng phải quay lại tay người.
        var moc = ReviewRules.HideTiesNewerThan(
            aiReviewEnabled: true, autoEnabled: true, NhipQuet, maxAttempts: 3, BayGio);

        var hoaVuaXong = BayGio.AddMinutes(-1);
        var hoaBoQuenLau = BayGio.AddMinutes(-30);

        Assert.True(hoaVuaXong > moc);       // còn tươi -> giấu
        Assert.False(hoaBoQuenLau > moc);    // quá hạn -> hiện lại
    }
}
