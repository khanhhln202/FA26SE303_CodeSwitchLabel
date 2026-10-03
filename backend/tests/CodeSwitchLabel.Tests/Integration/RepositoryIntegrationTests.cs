using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Sample integration tests demonstrating the test infrastructure.
/// These tests use the real PostgreSQL database via Testcontainers.
/// </summary>
[Collection("Database")]
public class CampaignRepositoryIntegrationTests : IntegrationTestBase
{
    public CampaignRepositoryIntegrationTests(PostgreSqlFixture fixture) : base(fixture)
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

/// <summary>
/// Integration tests for TaskRepository
/// </summary>
[Collection("Database")]
public class TaskRepositoryIntegrationTests : IntegrationTestBase
{
    public TaskRepositoryIntegrationTests(PostgreSqlFixture fixture) : base(fixture)
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

/// <summary>
/// Integration tests for UserRepository
/// </summary>
[Collection("Database")]
public class UserRepositoryIntegrationTests : IntegrationTestBase
{
    public UserRepositoryIntegrationTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsUser_WhenUserExists()
    {
        // Act
        var user = await Users.GetByEmailAsync("admin@test.local", CancellationToken.None);

        // Assert
        Assert.NotNull(user);
        Assert.Equal("admin@test.local", user.Email);
        Assert.Equal(RoleName.Admin, user.Role.RoleName);
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        // Act
        var user = await Users.GetByEmailAsync("nonexistent@test.local", CancellationToken.None);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsUserWithRole_WhenUserExists()
    {
        // Act
        var user = await Users.GetForUpdateAsync(AdminUserId, CancellationToken.None);

        // Assert
        Assert.NotNull(user);
        Assert.NotNull(user.Role);
        Assert.Equal(RoleName.Admin, user.Role.RoleName);
    }

    [Fact]
    public async Task CreateUser_PersistsUser()
    {
        // Arrange
        var email = $"newuser{Guid.NewGuid():N}@test.local";

        // Act
        var user = await CreateUserAsync(email, RoleName.Speaker, "New User");

        // Assert
        Assert.NotEqual(0, user.UserId);
        Assert.Equal(email, user.Email);

        var fetched = await Users.GetByEmailAsync(email, CancellationToken.None);
        Assert.NotNull(fetched);
        Assert.Equal(email, fetched.Email);
    }

    [Fact]
    public async Task SearchAsync_ReturnsUsers_WhenUsersExist()
    {
        // Act
        var (items, total) = await Users.SearchAsync(null, null, null, 1, 10, CancellationToken.None);

        // Assert
        Assert.True(total >= 4); // 4 seeded users
    }

    [Fact]
    public async Task GetByRoleAsync_ReturnsUsersWithRole()
    {
        // Act
        var reviewers = await Users.GetByRoleAsync(RoleName.Reviewer, CancellationToken.None);

        // Assert
        Assert.NotEmpty(reviewers);
        Assert.All(reviewers, u => Assert.Equal(RoleName.Reviewer, u.Role.RoleName));
    }

    [Fact]
    public async Task GetOpenTasksAsync_ReturnsEmpty_WhenNoTasks()
    {
        // Act
        var tasks = await Users.GetOpenTasksAsync(SpeakerUserId, CancellationToken.None);

        // Assert
        Assert.NotNull(tasks);
        Assert.Empty(tasks);
    }
}

/// <summary>
/// Integration tests for RecordingRepository
/// </summary>
[Collection("Database")]
public class RecordingRepositoryIntegrationTests : IntegrationTestBase
{
    public RecordingRepositoryIntegrationTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetAsync_ReturnsRecording_WhenRecordingExists()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]hello [vi]world",
            "[vi]Test chào thế giới",
            ScriptDomain.ItTechnology,
            AdminUserId);

        var recording = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        // Act
        var result = await Recordings.GetAsync(recording.RecordingId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(recording.RecordingId, result.RecordingId);
    }

    [Fact]
    public async Task SearchAsync_ReturnsRecordings_WhenRecordingsExist()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]record [vi]this",
            "[vi]Test ghi âm cái này",
            ScriptDomain.ItTechnology,
            AdminUserId);

        await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching, 7.5m);

        // Act
        var (items, total) = await Recordings.SearchAsync(null, null, null, 1, 10, CancellationToken.None);

        // Assert
        Assert.True(total >= 1);
    }

    [Fact]
    public async Task HasActiveRecordingAsync_ReturnsFalse_WhenNoRecording()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]active [vi]check",
            "[vi]Test kiểm tra active",
            ScriptDomain.ItTechnology,
            AdminUserId);

        // Act
        var hasActive = await Recordings.HasActiveRecordingAsync(script.ScriptId, SentenceVariant.CodeSwitching, CancellationToken.None);

        // Assert
        Assert.False(hasActive);
    }

    [Fact]
    public async Task HasActiveRecordingAsync_ReturnsTrue_WhenRecordingExists()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]active [vi]true",
            "[vi]Test kiểm tra active true",
            ScriptDomain.ItTechnology,
            AdminUserId);

        await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        // Act
        var hasActive = await Recordings.HasActiveRecordingAsync(script.ScriptId, SentenceVariant.CodeSwitching, CancellationToken.None);

        // Assert
        Assert.True(hasActive);
    }

    [Fact]
    public async Task GetOwnerSpeakerIdAsync_ReturnsSpeaker_WhenRecordingExists()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]owner [vi]id",
            "[vi]Test owner id",
            ScriptDomain.ItTechnology,
            AdminUserId);

        await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        // Act
        var ownerId = await Recordings.GetOwnerSpeakerIdAsync(script.ScriptId, CancellationToken.None);

        // Assert
        Assert.Equal(SpeakerUserId, ownerId);
    }

    [Fact]
    public async Task CountTakesAsync_ReturnsCount()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]count [vi]takes",
            "[vi]Test đếm lượt",
            ScriptDomain.ItTechnology,
            AdminUserId);

        await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        // Act
        var count = await Recordings.CountTakesAsync(script.ScriptId, SentenceVariant.CodeSwitching, CancellationToken.None);

        // Assert
        Assert.Equal(1, count);
    }
}

/// <summary>
/// Integration tests for ReviewRepository
/// </summary>
[Collection("Database")]
public class ReviewRepositoryIntegrationTests : IntegrationTestBase
{
    public ReviewRepositoryIntegrationTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetReviewsAsync_ReturnsEmpty_WhenNoReviews()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]review [vi]this",
            "[vi]Test duyệt cái này",
            ScriptDomain.ItTechnology,
            AdminUserId);

        var recording = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        // Act
        var reviews = await Reviews.GetReviewsAsync(recording.RecordingId, CancellationToken.None);

        // Assert
        Assert.NotNull(reviews);
        Assert.Empty(reviews);
    }

    [Fact]
    public async Task GetNextForReviewerAsync_ReturnsRecording_WhenAvailable()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]next [vi]recording",
            "[vi]Test bản ghi tiếp theo",
            ScriptDomain.ItTechnology,
            AdminUserId);

        var recording = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        // Act
        var result = await Reviews.GetNextForReviewerAsync(
            ReviewerUserId, null, null, false, 3, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(recording.RecordingId, result.RecordingId);
    }

    [Fact]
    public async Task GetWithScriptAsync_ReturnsRecordingWithScript()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]with [vi]script",
            "[vi]Test có script",
            ScriptDomain.ItTechnology,
            AdminUserId);

        var recording = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        // Act
        var result = await Reviews.GetWithScriptAsync(recording.RecordingId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Script);
        Assert.Equal(script.ScriptId, result.Script.ScriptId);
    }

    [Fact]
    public async Task GetRecordingStatusAsync_ReturnsPendingReview_ForNewRecording()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]status [vi]check",
            "[vi]Test trạng thái",
            ScriptDomain.ItTechnology,
            AdminUserId);

        var recording = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        // Act
        var status = await Reviews.GetRecordingStatusAsync(recording.RecordingId, CancellationToken.None);

        // Assert
        Assert.Equal(RecordingStatus.PendingReview, status);
    }

    [Fact]
    public async Task LockRecordingAsync_ReturnsRecording_WhenExists()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]lock [vi]recording",
            "[vi]Test khóa bản ghi",
            ScriptDomain.ItTechnology,
            AdminUserId);

        var recording = await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        // Act
        await using var transaction = await Reviews.BeginTransactionAsync(CancellationToken.None);
        var locked = await Reviews.LockRecordingAsync(recording.RecordingId, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(locked);
        Assert.Equal(recording.RecordingId, locked.RecordingId);
    }
}

/// <summary>
/// Integration tests for ScriptRepository
/// </summary>
[Collection("Database")]
public class ScriptRepositoryIntegrationTests : IntegrationTestBase
{
    public ScriptRepositoryIntegrationTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetAsync_ReturnsScript_WhenScriptExists()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]script [vi]here",
            "[vi]Test kịch bản ở đây",
            ScriptDomain.ItTechnology,
            AdminUserId);

        // Act
        var result = await Scripts.GetAsync(script.ScriptId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(script.ScriptId, result.ScriptId);
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsScriptWithWords()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]for [vi]update",
            "[vi]Test để cập nhật",
            ScriptDomain.ItTechnology,
            AdminUserId);

        // Act
        var result = await Scripts.GetForUpdateAsync(script.ScriptId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Words);
    }

    [Fact]
    public async Task GetWithReviewsAsync_ReturnsScriptWithReviews()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]reviews [vi]here",
            "[vi]Test có reviews",
            ScriptDomain.ItTechnology,
            AdminUserId);

        // Act
        var result = await Scripts.GetWithReviewsAsync(script.ScriptId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SearchAsync_ReturnsScripts_WhenScriptsExist()
    {
        // Arrange
        await CreateScriptAsync(
            "[vi]Search [en]test [vi]one",
            "[vi]Tìm kiếm test một",
            ScriptDomain.ItTechnology,
            AdminUserId);

        await CreateScriptAsync(
            "[vi]Search [en]test [vi]two",
            "[vi]Tìm kiếm test hai",
            ScriptDomain.Education,
            AdminUserId);

        // Act
        var (items, total) = await Scripts.SearchAsync(null, null, "test", 1, 10, CancellationToken.None);

        // Assert
        Assert.True(total >= 2);
        Assert.Contains(items, s => s.CsContent.Contains("test"));
    }

    [Fact]
    public async Task ContentExistsAsync_ReturnsTrue_WhenContentExists()
    {
        // Arrange
        await CreateScriptAsync(
            "[vi]Unique [en]content [vi]here",
            "[vi]Nội dung duy nhất",
            ScriptDomain.ItTechnology,
            AdminUserId);

        // Act
        var exists = await Scripts.ContentExistsAsync("[vi]Unique [en]content [vi]here", CancellationToken.None);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ContentExistsAsync_ReturnsFalse_WhenContentDoesNotExist()
    {
        // Act
        var exists = await Scripts.ContentExistsAsync("[vi]Non [en]existent [vi]content", CancellationToken.None);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task HasRecordingsAsync_ReturnsFalse_WhenNoRecordings()
    {
        // Arrange
        var script = await CreateScriptAsync(
            "[vi]Test [en]no [vi]recordings",
            "[vi]Test không có bản ghi",
            ScriptDomain.ItTechnology,
            AdminUserId);

        // Act
        var hasRecordings = await Scripts.HasRecordingsAsync(script.ScriptId, CancellationToken.None);

        // Assert
        Assert.False(hasRecordings);
    }
}