using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Hai mảnh thuần của chế độ máy duyệt tự động: đọc cấu hình, và đếm số lần thử hỏng.
/// Tách ra khỏi tiến trình nền để test được mà không phải dựng host hay chờ đồng hồ.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AiReviewAutoTests
{
    private static SystemConfigDto Cfg(string key, string value) =>
        new(key, value, "string", null, DateTimeOffset.UtcNow);

    // ------------------------------------------------------------- đọc cấu hình

    [Fact]
    public void KhongCoCauHinh_ThiTatVaDungGiaTriMacDinh()
    {
        var settings = AiReviewAutoSettings.From([]);

        // Mặc định TẮT là cố ý: deploy một bản mới không được tự làm máy chủ bắt đầu
        // tiêu CPU cho nhận dạng giọng nói.
        Assert.False(settings.Enabled);
        Assert.Equal(AiReviewAutoSettings.DefaultInterval, settings.Interval);
        Assert.Equal(AiReviewAutoSettings.DefaultBatchSize, settings.BatchSize);
    }

    [Fact]
    public void BatCongTac_ThiChayVaDocDungKhoangNghiLanSoLuong()
    {
        var settings = AiReviewAutoSettings.From([
            Cfg(ConfigKeys.AiReviewAutoEnabled, "true"),
            Cfg(ConfigKeys.AiReviewAutoIntervalSeconds, "120"),
            Cfg(ConfigKeys.AiReviewAutoBatchSize, "5")
        ]);

        Assert.True(settings.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(120), settings.Interval);
        Assert.Equal(5, settings.BatchSize);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-30")]
    public void KhoangNghiQuaNgan_ThiKepLenMucToiThieu(string raw)
    {
        // Mỗi bản ghi tốn khoảng 50 giây nhận dạng, nên quét dày hơn không nhanh hơn được;
        // nó chỉ làm log đầy và quét database vô ích.
        var settings = AiReviewAutoSettings.From([Cfg(ConfigKeys.AiReviewAutoIntervalSeconds, raw)]);

        Assert.Equal(AiReviewAutoSettings.MinInterval, settings.Interval);
    }

    [Fact]
    public void KhoangNghiQuaDai_ThiKepXuongMucToiDa()
    {
        var settings = AiReviewAutoSettings.From([
            Cfg(ConfigKeys.AiReviewAutoIntervalSeconds, "999999")
        ]);

        Assert.Equal(AiReviewAutoSettings.MaxInterval, settings.Interval);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("-5", 1)]
    [InlineData("999", AiReviewAutoSettings.MaxBatchSize)]
    public void SoLuongMoiLuot_LuonNamTrongKhoangChoPhep(string raw, int mongDoi)
    {
        var settings = AiReviewAutoSettings.From([Cfg(ConfigKeys.AiReviewAutoBatchSize, raw)]);

        Assert.Equal(mongDoi, settings.BatchSize);
    }

    [Fact]
    public void CauHinhGoSai_ThiVeMacDinhChuKhongNemLoi()
    {
        // Một ô cấu hình gõ nhầm không đáng làm chết tiến trình nền.
        var settings = AiReviewAutoSettings.From([
            Cfg(ConfigKeys.AiReviewAutoEnabled, "bat"),
            Cfg(ConfigKeys.AiReviewAutoIntervalSeconds, "mỗi phút"),
            Cfg(ConfigKeys.AiReviewAutoBatchSize, "ba")
        ]);

        Assert.False(settings.Enabled);
        Assert.Equal(AiReviewAutoSettings.DefaultInterval, settings.Interval);
        Assert.Equal(AiReviewAutoSettings.DefaultBatchSize, settings.BatchSize);
    }

    // ------------------------------------------------------- bộ đếm thử lại

    [Fact]
    public void BanGhiMoi_ThiThuDuoc()
    {
        Assert.True(new AiReviewRetryBudget().CanTry("r_cs_1"));
    }

    [Fact]
    public void HongQuaSoLanChoPhep_ThiThoiKhongThuNua()
    {
        // Không có chốt này thì một bản ghi ASR không bao giờ đọc được sẽ ngốn khoảng
        // 50 giây CPU mỗi lượt quét, mãi mãi, và đẩy các bản lành phía sau phải chờ.
        var budget = new AiReviewRetryBudget(maxAttempts: 3);

        budget.RecordFailure("r_cs_1");
        budget.RecordFailure("r_cs_1");
        Assert.True(budget.CanTry("r_cs_1"));

        budget.RecordFailure("r_cs_1");
        Assert.False(budget.CanTry("r_cs_1"));
        Assert.Contains("r_cs_1", budget.GivenUp);
    }

    [Fact]
    public void ThanhCong_ThiXoaBoDemHong()
    {
        // Lần hỏng trước có thể chỉ là ASR trục trặc nhất thời, không phải bản ghi hỏng.
        var budget = new AiReviewRetryBudget(maxAttempts: 2);

        budget.RecordFailure("r_cs_1");
        budget.RecordSuccess("r_cs_1");
        budget.RecordFailure("r_cs_1");

        Assert.True(budget.CanTry("r_cs_1"));
        Assert.Equal(1, budget.Attempts("r_cs_1"));
    }

    [Fact]
    public void BoDemTachRiengTungBanGhi()
    {
        var budget = new AiReviewRetryBudget(maxAttempts: 1);

        budget.RecordFailure("r_cs_1");

        Assert.False(budget.CanTry("r_cs_1"));
        Assert.True(budget.CanTry("r_cs_2"));
    }
}
