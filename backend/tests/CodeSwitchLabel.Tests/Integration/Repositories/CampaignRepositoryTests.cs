using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;

namespace CodeSwitchLabel.Tests.Integration.Repositories;

/// <summary>Integration tests for CampaignRepository (real PostgreSQL via Testcontainers).</summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public class CampaignRepositoryTests : IntegrationTestBase
{
    public CampaignRepositoryTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SearchAsync_ReturnsCampaigns_WhenCampaignExists()
    {
        // Act
        var (items, total) = await Campaigns.SearchAsync(null, null, 1, 10, default, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(total >= 1);
        Assert.Contains(items, c => c.CampaignName == "Test Campaign");
    }

    [Fact]
    public async Task GetAsync_ReturnsCampaign_WhenCampaignExists()
    {
        // Act
        var campaign = await Campaigns.GetAsync(CampaignId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(campaign);
        Assert.Equal("Test Campaign", campaign.CampaignName);
        Assert.Equal(2000, campaign.TargetQty);
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsCampaign_WhenCampaignExists()
    {
        // Act
        var campaign = await Campaigns.GetForUpdateAsync(CampaignId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(campaign);
        Assert.Equal(CampaignId, campaign.CampaignId);
    }

    [Fact]
    public async Task SumAllocatedAsync_ReturnsZero_WhenNoTasks()
    {
        // Act
        var allocated = await Campaigns.SumAllocatedAsync(CampaignId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, allocated);
    }

    [Fact]
    public async Task IsTaskManagerAsync_ReturnsTrue_ForTaskManagerUser()
    {
        // Act
        var isManager = await Campaigns.IsTaskManagerAsync(ManagerUserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(isManager);
    }

    [Fact]
    public async Task IsTaskManagerAsync_ReturnsFalse_ForNonTaskManagerUser()
    {
        // Act
        var isManager = await Campaigns.IsTaskManagerAsync(ReviewerUserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(isManager);
    }

    [Fact]
    public async Task GetProgressAsync_ReturnsProgress_WhenCampaignExists()
    {
        // Act
        var progress = await Campaigns.GetProgressAsync([CampaignId], TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(progress);
        var p = Assert.Single(progress);
        Assert.Equal(CampaignId, p.CampaignId);
    }

    [Fact]
    public async Task CreateCampaign_AndSearch_ReturnsNewCampaign()
    {
        // Arrange
        var newCampaign = await CreateCampaignAsync(
            "New Campaign",
            2000,
            DateOnly.FromDateTime(DateTime.Today),
            DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
            AdminUserId,
            CampaignStatus.Open);

        // Act
        var (items, total) = await Campaigns.SearchAsync(null, null, 1, 10, default, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(items, c => c.CampaignName == "New Campaign");
        Assert.Equal(2000, items.First(c => c.CampaignName == "New Campaign").TargetQty);
    }

    // ---------------------------------------------------------------- TEAM_012: sắp xếp

    /// <summary>Ba chiến dịch có ngày bắt đầu khác nhau. Danh sách chiến dịch không lọc theo
    /// tên nên test lấy cả trang rồi chỉ giữ lại ba cái vừa tạo để so thứ tự tương đối.
    /// Chỉ tiêu phải nằm trong khoảng 2000..5000 mà lược đồ chặn.</summary>
    private async Task<Campaign[]> CreateThreeSortedCampaignsAsync()
    {
        var campaigns = new List<Campaign>();
        var firstStart = DateOnly.FromDateTime(DateTime.Today).AddDays(30);

        for (var i = 1; i <= 3; i++)
        {
            var start = firstStart.AddDays(i - 1);

            campaigns.Add(await CreateCampaignAsync(
                $"Sort Campaign {i}", 2000, start, start.AddDays(7), AdminUserId));
        }

        return [.. campaigns];
    }

    private static List<long> RelativeOrder(IEnumerable<Campaign> page, IReadOnlyCollection<Campaign> mine)
    {
        var ids = mine.Select(c => c.CampaignId).ToHashSet();
        return [.. page.Where(c => ids.Contains(c.CampaignId)).Select(c => c.CampaignId)];
    }

    [Fact]
    public async Task SearchAsync_KhongTruyenSort_GiuNguyenThuTuDotMoiNhatTruoc()
    {
        // Regression TEAM_012: mặc định của /api/campaigns là ngày bắt đầu giảm dần.
        var mine = await CreateThreeSortedCampaignsAsync();

        var (items, _) = await Campaigns.SearchAsync(
            null, null, 1, 50, default, TestContext.Current.CancellationToken);

        Assert.Equal(
            [mine[2].CampaignId, mine[1].CampaignId, mine[0].CampaignId],
            RelativeOrder(items, mine));
    }

    [Fact]
    public async Task SearchAsync_SapXepTheoStartDate_TangDan()
    {
        var mine = await CreateThreeSortedCampaignsAsync();

        var (items, _) = await Campaigns.SearchAsync(
            null, null, 1, 50,
            SortSpec<CampaignSortField>.From("startDate", "asc"), TestContext.Current.CancellationToken);

        Assert.Equal(
            [mine[0].CampaignId, mine[1].CampaignId, mine[2].CampaignId],
            RelativeOrder(items, mine));
    }

    [Fact]
    public async Task SearchAsync_SapXepTheoTen_DungChuoiTen()
    {
        var mine = await CreateThreeSortedCampaignsAsync();

        var (items, _) = await Campaigns.SearchAsync(
            null, null, 1, 50,
            SortSpec<CampaignSortField>.From("campaignName", "asc"), TestContext.Current.CancellationToken);

        // "Sort Campaign 1" < "Sort Campaign 2" < "Sort Campaign 3" — nhưng cột này KHÔNG có
        // trong CampaignSortField, nên tên cột sai phải rơi về thứ tự mặc định.
        Assert.Equal(
            [mine[2].CampaignId, mine[1].CampaignId, mine[0].CampaignId],
            RelativeOrder(items, mine));
    }
}
