using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Luật phán của máy duyệt: một ranh giới duy nhất, và máy LUÔN phải chọn một bên.
/// Máy chỉ vào khi hai Reviewer người lệch nhau, tức nó là người phá thế — bỏ quyền
/// lúc đó để lại bản ghi 1–1 không ai quyết.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AiReviewEvaluatorTests
{
    private static AiReviewSettings Settings(decimal threshold = 0.40m) =>
        new(Enabled: true, threshold);

    [Theory]
    [InlineData(0.40)] // đúng ranh giới cũng từ chối: luật là ">= ngưỡng"
    [InlineData(0.55)]
    [InlineData(1.00)]
    [InlineData(2.00)] // đọc lan sang câu khác
    public void WerTuRanhGioiTroLen_ThiTuChoiVoiLyDoSaiNoiDung(decimal wer)
    {
        var decision = AiReviewEvaluator.Evaluate(wer, Settings());

        Assert.Equal(AiReviewVerdict.Rejected, decision.Verdict);
        Assert.Equal([AiReviewEvaluator.ContentMismatch], decision.RejectionReasonCodes);
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(0.05)]
    [InlineData(0.25)] // vùng từng là "không chắc" — giờ phải quyết, và quyết là đạt
    [InlineData(0.39)]
    public void WerDuoiRanhGioi_ThiDuyetDat(decimal wer)
    {
        var decision = AiReviewEvaluator.Evaluate(wer, Settings());

        Assert.Equal(AiReviewVerdict.Approved, decision.Verdict);
        Assert.Empty(decision.RejectionReasonCodes);
    }

    [Fact]
    public void KhongBaoGioTraVeTrangThaiThuBa()
    {
        // Chốt bằng kiểu dữ liệu: enum chỉ có hai giá trị, nên không thể lọt "bỏ quyền".
        var values = Enum.GetValues<AiReviewVerdict>();

        Assert.Equal(2, values.Length);
        Assert.Contains(AiReviewVerdict.Approved, values);
        Assert.Contains(AiReviewVerdict.Rejected, values);
    }

    [Fact]
    public void GiaiThich_NeuConSoVaNoiRoViSaoMayVao()
    {
        var decision = AiReviewEvaluator.Evaluate(0.55m, Settings());

        Assert.Contains("0.55", decision.Explanation);
        Assert.Contains("0.4", decision.Explanation);
        Assert.Contains("phiếu thứ ba", decision.Explanation);
    }

    [Fact]
    public void DoiRanhGioi_ThiKetQuaDoiTheo()
    {
        // Cùng một WER, hai ranh giới khác nhau, hai kết quả khác nhau.
        Assert.Equal(AiReviewVerdict.Approved, AiReviewEvaluator.Evaluate(0.30m, Settings(0.40m)).Verdict);
        Assert.Equal(AiReviewVerdict.Rejected, AiReviewEvaluator.Evaluate(0.30m, Settings(0.20m)).Verdict);
    }

    [Fact]
    public void DocCauHinh_LayDungGiaTriTuSystemConfig()
    {
        var settings = AiReviewSettings.From([
            Config(ConfigKeys.AiReviewEnabled, "true"),
            Config(ConfigKeys.AiReviewWerDecisionThreshold, "0.55")
        ]);

        Assert.True(settings.Enabled);
        Assert.Equal(0.55m, settings.WerDecisionThreshold);
    }

    [Fact]
    public void DocCauHinh_ThieuKhoaThiTatVaDungNguongMacDinh()
    {
        var settings = AiReviewSettings.From([]);

        Assert.False(settings.Enabled);
        Assert.Equal(AiReviewSettings.DefaultThreshold, settings.WerDecisionThreshold);
    }

    [Fact]
    public void DocCauHinh_GiaTriRac_ThiDungMacDinhChuKhongNem()
    {
        var settings = AiReviewSettings.From([
            Config(ConfigKeys.AiReviewEnabled, "có"),
            Config(ConfigKeys.AiReviewWerDecisionThreshold, "rất cao")
        ]);

        Assert.False(settings.Enabled);

        // So với hằng số chứ không với con số: thứ cần khẳng định ở đây là "có lùi về
        // mặc định", không phải "mặc định bằng bao nhiêu". Ghi cứng giá trị vào đây chỉ
        // làm test gãy mỗi lần luật nghiệp vụ đổi, mà chẳng bắt được lỗi nào.
        Assert.Equal(AiReviewSettings.DefaultThreshold, settings.WerDecisionThreshold);
    }

    /// <summary>
    /// Gác cái bẫy của ngưỡng 0. Luật so là <c>effectiveWer &gt;= ngưỡng</c>, nên đặt
    /// ngưỡng đúng bằng 0 sẽ làm <c>0 &gt;= 0</c> đúng và từ chối CẢ bản ghi đọc khớp hoàn
    /// hảo — tức từ chối tất cả, không phải "sai một từ là từ chối".
    ///
    /// <para>
    /// Test này khẳng định ranh giới chứ không khẳng định con số: đổi
    /// <see cref="AiReviewSettings.DefaultThreshold"/> sang giá trị khác vẫn xanh, nhưng
    /// đổi về 0 là đỏ ngay.
    /// </para>
    /// </summary>
    [Fact]
    public void NguongMacDinh_KhopHoanHaoThiDat_SaiDuMotTuThiTuChoi()
    {
        var macDinh = Settings(AiReviewSettings.DefaultThreshold);

        Assert.Equal(AiReviewVerdict.Approved, AiReviewEvaluator.Evaluate(0m, macDinh).Verdict);

        // 0.001 = sai một từ trong kịch bản 1000 từ, tức ca sai-ít-nhất mà ta có thể gặp.
        Assert.Equal(AiReviewVerdict.Rejected, AiReviewEvaluator.Evaluate(0.001m, macDinh).Verdict);

        // 0.1 = sai một từ trong câu 10 từ, cỡ câu thật của đồ án.
        Assert.Equal(AiReviewVerdict.Rejected, AiReviewEvaluator.Evaluate(0.1m, macDinh).Verdict);
    }

    private static SystemConfigDto Config(string key, string value) =>
        new(key, value, "string", null, DateTimeOffset.UtcNow);
}
