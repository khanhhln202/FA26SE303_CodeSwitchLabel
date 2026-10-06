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
        var (items, total) = await Campaigns.SearchAsync(null, null, 1, 10, TestContext.Current.CancellationToken);

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
            CampaignStatus.Draft);

        // Act
        var (items, total) = await Campaigns.SearchAsync(null, null, 1, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(items, c => c.CampaignName == "New Campaign");
        Assert.Equal(2000, items.First(c => c.CampaignName == "New Campaign").TargetQty);
    }
}
