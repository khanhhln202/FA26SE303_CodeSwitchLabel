using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Reviews;

namespace CodeSwitchLabel.Tests;

/// <summary>
/// Luật nhiều vòng duyệt là phần dễ sai nhất của module Review, và sai thì dataset
/// nhận bản ghi không đạt mà không ai hay. Mỗi nhánh của luật có đúng một test.
/// </summary>
public class ReviewStateMachineTests
{
    private const ReviewDecision Ok = ReviewDecision.Approved;
    private const ReviewDecision No = ReviewDecision.Rejected;

    [Theory]
    [InlineData(Ok, RecordingStatus.Approved)]
    [InlineData(No, RecordingStatus.Rejected)]
    public void Vong1_KhongBiRutMau_ChotLuon(ReviewDecision decision, RecordingStatus expected)
    {
        Assert.Equal(expected, ReviewStateMachine.NextStatus(1, decision, [], selectedForSpotCheck: false));
    }

    /// <summary>
    /// Nhánh quan trọng nhất: bị rút mẫu thì KHÔNG được chốt sớm. Chốt sớm rồi vòng 2 lật lại
    /// nghĩa là bản ghi có thể đã nằm trong dataset trước khi bị phát hiện là không đạt.
    /// </summary>
    [Theory]
    [InlineData(Ok)]
    [InlineData(No)]
    public void Vong1_BiRutMau_VanChoDuyet_KhongChotSom(ReviewDecision decision)
    {
        Assert.Equal(
            RecordingStatus.PendingReview,
            ReviewStateMachine.NextStatus(1, decision, [], selectedForSpotCheck: true));
    }

    [Theory]
    [InlineData(Ok, RecordingStatus.Approved)]
    [InlineData(No, RecordingStatus.Rejected)]
    public void Vong2_TrungYVong1_Chot(ReviewDecision both, RecordingStatus expected)
    {
        Assert.Equal(expected, ReviewStateMachine.NextStatus(2, both, [both], selectedForSpotCheck: false));
    }

    [Theory]
    [InlineData(Ok, No)]
    [InlineData(No, Ok)]
    public void Vong2_LechVong1_ChoVong3PhanXu(ReviewDecision round1, ReviewDecision round2)
    {
        Assert.Equal(
            RecordingStatus.PendingReview,
            ReviewStateMachine.NextStatus(2, round2, [round1], selectedForSpotCheck: false));
    }

    [Theory]
    [InlineData(Ok, No, No, RecordingStatus.Rejected)]
    [InlineData(No, Ok, Ok, RecordingStatus.Approved)]
    [InlineData(Ok, No, Ok, RecordingStatus.Approved)]
    public void Vong3_ChotTheoNguoiPhanXu(
        ReviewDecision round1, ReviewDecision round2, ReviewDecision round3, RecordingStatus expected)
    {
        Assert.Equal(expected, ReviewStateMachine.NextStatus(3, round3, [round1, round2], selectedForSpotCheck: false));
    }

    [Fact]
    public void Vong3_KhiHaiVongTruocDaTrungY_LaLoiLogic()
    {
        Assert.Throws<InvalidOperationException>(
            () => ReviewStateMachine.NextStatus(3, Ok, [Ok, Ok], selectedForSpotCheck: false));
    }

    [Fact]
    public void SoQuyetDinhTruocKhongKhopVong_LaLoiLogic()
    {
        Assert.Throws<ArgumentException>(
            () => ReviewStateMachine.NextStatus(2, Ok, [], selectedForSpotCheck: false));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void ChiVongKiemTraLaDuyetMu(int round, bool expectedBlind)
    {
        Assert.Equal(expectedBlind, ReviewStateMachine.IsBlind(round));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void VongNgoaiKhoang1Den3_BiTuChoi(int round)
    {
        // Khớp ràng buộc CHECK review_round BETWEEN 1 AND 3 dưới database.
        Assert.Throws<ArgumentOutOfRangeException>(() => ReviewStateMachine.KindOf(round));
    }

    [Fact]
    public void BoRutMau_TiLe0_KhongBaoGioRut_TiLe1_LuonRut()
    {
        var sampler = new RandomReviewSampler();

        Assert.All(Enumerable.Range(0, 500), _ => Assert.False(sampler.ShouldSpotCheck(0m)));
        Assert.All(Enumerable.Range(0, 500), _ => Assert.True(sampler.ShouldSpotCheck(1m)));
    }
}
