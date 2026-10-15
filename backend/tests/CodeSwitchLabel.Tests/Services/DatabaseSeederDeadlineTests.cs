using CodeSwitchLabel.Services.Seeding;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Hạn của task mẫu phải nằm trong cửa sổ ngày của chiến dịch, và trigger
/// trg_task_campaign_window so giờ Việt Nam. Sai múi giờ ở đây thì seeder ném exception
/// lúc khởi động và API không dựng được trên database mới — chỉ lộ ra lúc chạy thật.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DatabaseSeederDeadlineTests
{
    /// <summary>Giữ đúng biên trigger: hạn phải < (ngày cuối + 1) lúc 00:00 giờ Việt Nam.</summary>
    private static DateTimeOffset WindowEnd(DateOnly endDate) =>
        new DateTimeOffset(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DatabaseSeeder.CampaignTimeZone).ToUniversalTime();

    [Fact]
    public void HanCuoiChiDich_ThuocKhungThoiGianCuaChienDich()
    {
        var endDate = new DateOnly(2026, 11, 13);

        var deadline = DatabaseSeeder.EndOfCampaignDay(endDate);

        Assert.True(deadline < WindowEnd(endDate), $"{deadline:o} phải nhỏ hơn {WindowEnd(endDate):o}");
    }

    /// <summary>Ngày cuối phải lấy trọn vẹn, không sớm hơn 23:59:59 giờ Việt Nam.</summary>
    [Fact]
    public void HanCuoiChiDich_LayTronNgayCuoi()
    {
        var endDate = new DateOnly(2026, 11, 13);

        var expected = new DateTimeOffset(endDate.ToDateTime(new TimeOnly(23, 59, 59)), TimeSpan.FromHours(7));

        Assert.Equal(expected, DatabaseSeeder.EndOfCampaignDay(endDate));
    }

    /// <summary>Npgsql từ chối DateTimeOffset lệch UTC khi ghi vào timestamptz.</summary>
    [Fact]
    public void HanCuoiChiDich_TraVeLichGioUtc()
    {
        var deadline = DatabaseSeeder.EndOfCampaignDay(new DateOnly(2026, 11, 13));

        Assert.Equal(TimeSpan.Zero, deadline.Offset);
    }
}