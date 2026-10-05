using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;

namespace CodeSwitchLabel.Tests.Integration.Repositories;

/// <summary>Integration tests for RecordingRepository.</summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public class RecordingRepositoryTests : IntegrationTestBase
{
    public RecordingRepositoryTests(DatabaseFixture fixture) : base(fixture)
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
