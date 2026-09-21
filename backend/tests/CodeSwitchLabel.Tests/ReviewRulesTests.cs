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
}
