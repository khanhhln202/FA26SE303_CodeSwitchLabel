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
        var (items, total) = await Campaigns.SearchAsync(null, null, 1, 10, CancellationToken.None);

        // Assert
        Assert.True(total >= 1);
        Assert.Contains(items, c => c.CampaignName == "Test Campaign");
    }

    [Fact]
    public async Task GetAsync_ReturnsCampaign_WhenCampaignExists()
    {
        // Act
        var campaign = await Campaigns.GetAsync(CampaignId, CancellationToken.None);

        // Assert
        Assert.NotNull(campaign);
        Assert.Equal("Test Campaign", campaign.CampaignName);
        Assert.Equal(2000, campaign.TargetQty);
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsCampaign_WhenCampaignExists()
    {
        // Act
        var campaign = await Campaigns.GetForUpdateAsync(CampaignId, CancellationToken.None);

        // Assert
        Assert.NotNull(campaign);
        Assert.Equal(CampaignId, campaign.CampaignId);
    }

    [Fact]
    public async Task SumAllocatedAsync_ReturnsZero_WhenNoTasks()
    {
        // Act
        var allocated = await Campaigns.SumAllocatedAsync(CampaignId, CancellationToken.None);

        // Assert
        Assert.Equal(0, allocated);
    }

    [Fact]
    public async Task IsTaskManagerAsync_ReturnsTrue_ForTaskManagerUser()
    {
        // Act
        var isManager = await Campaigns.IsTaskManagerAsync(ManagerUserId, CancellationToken.None);

        // Assert
        Assert.True(isManager);
    }

    [Fact]
    public async Task IsTaskManagerAsync_ReturnsFalse_ForNonTaskManagerUser()
    {
        // Act
        var isManager = await Campaigns.IsTaskManagerAsync(ReviewerUserId, CancellationToken.None);

        // Assert
        Assert.False(isManager);
    }

    [Fact]
    public async Task GetProgressAsync_ReturnsProgress_WhenCampaignExists()
    {
        // Act
        var progress = await Campaigns.GetProgressAsync([CampaignId], CancellationToken.None);

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
        var (items, total) = await Campaigns.SearchAsync(null, null, 1, 10, CancellationToken.None);

        // Assert
        Assert.Contains(items, c => c.CampaignName == "New Campaign");
        Assert.Equal(2000, items.First(c => c.CampaignName == "New Campaign").TargetQty);
    }
}
