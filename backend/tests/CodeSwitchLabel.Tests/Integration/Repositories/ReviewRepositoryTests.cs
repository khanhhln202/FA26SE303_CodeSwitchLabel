using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;

namespace CodeSwitchLabel.Tests.Integration.Repositories;

/// <summary>Integration tests for ReviewRepository.</summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public class ReviewRepositoryTests : IntegrationTestBase
{
    public ReviewRepositoryTests(DatabaseFixture fixture) : base(fixture)
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

        // Act — IntegrationTestBase đã mở transaction cho cả bài test, nên không mở thêm:
        // mở chồng transaction trên cùng connection là lỗi của Npgsql/EF.
        var locked = await Reviews.LockRecordingAsync(recording.RecordingId, CancellationToken.None);

        // Assert
        Assert.NotNull(locked);
        Assert.Equal(recording.RecordingId, locked.RecordingId);
    }
}
