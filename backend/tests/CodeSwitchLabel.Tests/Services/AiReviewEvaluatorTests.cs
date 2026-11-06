using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Luật phán của máy duyệt: trên ngưỡng thì từ chối, dưới ngưỡng thì đủ điều kiện đạt,
/// ở giữa thì bỏ quyền. Và mặc định máy KHÔNG bỏ phiếu đạt.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AiReviewEvaluatorTests
{
    private static AiReviewSettings Settings(
        decimal rejectMin = 0.40m, decimal approveMax = 0.10m, bool allowApprove = false) =>
        new(Enabled: true, rejectMin, approveMax, allowApprove);

    [Theory]
    [InlineData(0.40)] // đúng ngưỡng cũng từ chối: luật là ">= ngưỡng"
    [InlineData(0.55)]
    [InlineData(2.00)] // đọc lan sang câu khác
    public void WerTuNguongTroLen_ThiTuChoiVoiLyDoSaiNoiDung(decimal wer)
    {
        var decision = AiReviewEvaluator.Evaluate(wer, Settings());

        Assert.Equal(AiReviewVerdict.Rejected, decision.Verdict);
        Assert.Equal([AiReviewEvaluator.ContentMismatch], decision.RejectionReasonCodes);
    }

    [Fact]
    public void WerThap_NhungChuaBatChoBoPhieuDat_ThiBoQuyen()
    {
        var decision = AiReviewEvaluator.Evaluate(0.05m, Settings(allowApprove: false));

        Assert.Equal(AiReviewVerdict.Abstained, decision.Verdict);
        Assert.Empty(decision.RejectionReasonCodes);
        Assert.Contains("không cho máy bỏ phiếu đạt", decision.Explanation);
    }

    [Fact]
    public void WerThap_VaDaBatChoBoPhieuDat_ThiDuyetDat()
    {
        var decision = AiReviewEvaluator.Evaluate(0.05m, Settings(allowApprove: true));

        Assert.Equal(AiReviewVerdict.Approved, decision.Verdict);
        Assert.Empty(decision.RejectionReasonCodes);
    }

    [Theory]
    [InlineData(0.11)]
    [InlineData(0.25)]
    [InlineData(0.39)]
    public void WerTrongVungMoHo_ThiBoQuyen(decimal wer)
    {
        var decision = AiReviewEvaluator.Evaluate(wer, Settings(allowApprove: true));

        Assert.Equal(AiReviewVerdict.Abstained, decision.Verdict);
        Assert.Empty(decision.RejectionReasonCodes);
    }

    [Fact]
    public void HaiNguongDatChongNhau_ThiVungChongRoiVeTuChoi()
    {
        // Cấu hình sai (reject_min thấp hơn approve_max). Hướng sai an toàn hơn là từ chối:
        // từ chối oan thì chỉ phải thu lại, duyệt oan thì dữ liệu rác vào dataset.
        var decision = AiReviewEvaluator.Evaluate(
            0.20m, Settings(rejectMin: 0.10m, approveMax: 0.40m, allowApprove: true));

        Assert.Equal(AiReviewVerdict.Rejected, decision.Verdict);
    }

    [Fact]
    public void GiaiThich_LuonNeuConSoDeNguoiDocTraLaiDuoc()
    {
        var decision = AiReviewEvaluator.Evaluate(0.55m, Settings());

        Assert.Contains("0.55", decision.Explanation);
        Assert.Contains("0.4", decision.Explanation);
    }

    [Fact]
    public void DocCauHinh_LayDungGiaTriTuSystemConfig()
    {
        var settings = AiReviewSettings.From([
            Config(ConfigKeys.AiReviewEnabled, "true"),
            Config(ConfigKeys.AiReviewWerRejectMin, "0.55"),
            Config(ConfigKeys.AiReviewWerApproveMax, "0.15"),
            Config(ConfigKeys.AiReviewAllowApproveVote, "true")
        ]);

        Assert.True(settings.Enabled);
        Assert.Equal(0.55m, settings.WerRejectMin);
        Assert.Equal(0.15m, settings.WerApproveMax);
        Assert.True(settings.AllowApproveVote);
    }

    [Fact]
    public void DocCauHinh_ThieuKhoaThiDungMacDinhAnToan()
    {
        var settings = AiReviewSettings.From([]);

        Assert.False(settings.Enabled);
        Assert.False(settings.AllowApproveVote);
        Assert.Equal(0.40m, settings.WerRejectMin);
        Assert.Equal(0.10m, settings.WerApproveMax);
    }

    [Fact]
    public void DocCauHinh_GiaTriRac_ThiDungMacDinhChuKhongNem()
    {
        var settings = AiReviewSettings.From([
            Config(ConfigKeys.AiReviewWerRejectMin, "rất cao"),
            Config(ConfigKeys.AiReviewAllowApproveVote, "có")
        ]);

        Assert.Equal(0.40m, settings.WerRejectMin);
        Assert.False(settings.AllowApproveVote);
    }

    private static SystemConfigDto Config(string key, string value) =>
        new(key, value, "string", null, DateTimeOffset.UtcNow);
}
