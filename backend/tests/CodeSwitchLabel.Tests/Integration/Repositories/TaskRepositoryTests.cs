using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;

namespace CodeSwitchLabel.Tests.Integration.Repositories;

/// <summary>Integration tests for TaskRepository.</summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public class TaskRepositoryTests : IntegrationTestBase
{
    public TaskRepositoryTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsTask_WhenTaskExists()
    {
        // Arrange
        var task = await CreateTaskAsync(CampaignId, TaskType.Recording, 50, ManagerUserId);

        // Act
        var result = await Tasks.GetForUpdateAsync(task.TaskId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(task.TaskId, result.TaskId);
        Assert.Equal(50, result.TargetQty);
    }

    [Fact]
    public async Task GetRowAsync_ReturnsTaskRow_WhenTaskExists()
    {
        // Arrange
        var task = await CreateTaskAsync(CampaignId, TaskType.Recording, 50, ManagerUserId);

        // Act
        var result = await Tasks.GetRowAsync(task.TaskId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(task.TaskId, result.TaskId);
        Assert.Equal(50, result.TargetQty);
        Assert.Equal(0, result.Done);
    }

    [Fact]
    public async Task SearchAsync_ReturnsTasks_WhenTasksExist()
    {
        // Arrange
        await CreateTaskAsync(CampaignId, TaskType.Recording, 50, ManagerUserId);
        await CreateTaskAsync(CampaignId, TaskType.Review, 30, ManagerUserId);

        // Act
        var (items, total) = await Tasks.SearchAsync(null, null, null, null, DateTimeOffset.UtcNow, 1, 10, CancellationToken.None);

        // Assert
        Assert.True(total >= 2);
    }

    [Fact]
    public async Task GetActiveRowsForAssigneeAsync_ReturnsEmpty_WhenNoAssignments()
    {
        // Act
        var items = await Tasks.GetActiveRowsForAssigneeAsync(ManagerUserId, TaskType.Recording, CancellationToken.None);

        // Assert
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task IsActiveTaskOfAsync_ReturnsFalse_WhenNotAssigned()
    {
        // Arrange
        var task = await CreateTaskAsync(CampaignId, TaskType.Recording, 50, ManagerUserId);

        // Act
        var result = await Tasks.IsActiveTaskOfAsync(task.TaskId, ReviewerUserId, TaskType.Recording, CancellationToken.None);

        // Assert
        Assert.False(result);
    }
}
