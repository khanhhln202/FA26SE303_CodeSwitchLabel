using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;

namespace CodeSwitchLabel.Tests.Integration.Repositories;

/// <summary>Integration tests for ScriptRepository.</summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public class ScriptRepositoryTests : IntegrationTestBase
{
    public ScriptRepositoryTests(DatabaseFixture fixture) : base(fixture)
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
