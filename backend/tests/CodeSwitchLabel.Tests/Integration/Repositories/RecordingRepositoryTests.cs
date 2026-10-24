using CodeSwitchLabel.Repositories.Entities;
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
        var result = await Recordings.GetAsync(recording.RecordingId, TestContext.Current.CancellationToken);

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
        var (items, total) = await Recordings.SearchAsync(null, null, null, 1, 10, default, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(total >= 1);
    }

    // ---------------------------------------------------------------- sắp xếp

    /// <summary>
    /// Ba bản ghi của một người đọc riêng, đặt mốc thu và thời lượng khác nhau. Mốc đặt tay
    /// vì UtcNow trên Windows chỉ chính xác 15ms — hai bản tạo liền có thể trùng mốc.
    /// </summary>
    private async Task<(long SpeakerId, Recording[] Recordings)> CreateThreeSortedRecordingsAsync()
    {
        var speaker = await CreateUserAsync(NewUniqueEmail("sortspk"), RoleName.Speaker);
        var token = Guid.NewGuid().ToString("N")[..10];
        var baseTime = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

        var recordings = new List<Recording>();
        for (var i = 0; i < 3; i++)
        {
            var script = await CreateScriptAsync(
                $"[vi]Ban [en]ghi {token} so {i}", $"[vi]Bản ghi {token} số {i}",
                ScriptDomain.ItTechnology, AdminUserId);

            var recording = await CreateRecordingAsync(
                script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching, durationSec: 3m + i);

            recording.RecordedAt = baseTime.AddHours(i);
            recordings.Add(recording);
        }

        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (speaker.UserId, [.. recordings]);
    }

    [Fact]
    public async Task SearchAsync_KhongTruyenSort_GiuNguyenThuTuBanGhiMoiNhatTruoc()
    {
        // Regression không gửi sort thì phải y hệt trước khi có tính năng này.
        var (speakerId, recordings) = await CreateThreeSortedRecordingsAsync();

        var (items, total) = await Recordings.SearchAsync(
            speakerId, null, null, 1, 10, default, TestContext.Current.CancellationToken);

        Assert.Equal(3, total);
        Assert.Equal(
            [recordings[2].RecordingId, recordings[1].RecordingId, recordings[0].RecordingId],
            items.Select(r => r.RecordingId));
    }

    [Fact]
    public async Task SearchAsync_SapXepTheoDurationSec_TangDanVaGiamDan()
    {
        var (speakerId, recordings) = await CreateThreeSortedRecordingsAsync();

        var (asc, _) = await Recordings.SearchAsync(
            speakerId, null, null, 1, 10,
            SortSpec<RecordingSortField>.From("durationSec", "asc"), TestContext.Current.CancellationToken);

        var (desc, _) = await Recordings.SearchAsync(
            speakerId, null, null, 1, 10,
            SortSpec<RecordingSortField>.From("durationSec", "desc"), TestContext.Current.CancellationToken);

        Assert.Equal(
            [recordings[0].RecordingId, recordings[1].RecordingId, recordings[2].RecordingId],
            asc.Select(r => r.RecordingId));
        Assert.Equal(
            [recordings[2].RecordingId, recordings[1].RecordingId, recordings[0].RecordingId],
            desc.Select(r => r.RecordingId));
    }

    [Fact]
    public async Task SearchAsync_SapXepTheoRecordedAt_TangDan()
    {
        var (speakerId, recordings) = await CreateThreeSortedRecordingsAsync();

        var (items, _) = await Recordings.SearchAsync(
            speakerId, null, null, 1, 10,
            SortSpec<RecordingSortField>.From("recordedAt", "asc"), TestContext.Current.CancellationToken);

        Assert.Equal(
            [recordings[0].RecordingId, recordings[1].RecordingId, recordings[2].RecordingId],
            items.Select(r => r.RecordingId));
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
        var hasActive = await Recordings.HasActiveRecordingAsync(script.ScriptId, SentenceVariant.CodeSwitching, TestContext.Current.CancellationToken);

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
        var hasActive = await Recordings.HasActiveRecordingAsync(script.ScriptId, SentenceVariant.CodeSwitching, TestContext.Current.CancellationToken);

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
        var ownerId = await Recordings.GetOwnerSpeakerIdAsync(script.ScriptId, TestContext.Current.CancellationToken);

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
        var count = await Recordings.CountTakesAsync(script.ScriptId, SentenceVariant.CodeSwitching, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
    }
}
