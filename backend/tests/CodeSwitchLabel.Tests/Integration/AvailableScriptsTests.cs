using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Số câu còn dùng được của một chủ đề: đã duyệt nội dung và chưa có bản ghi
/// đang chờ duyệt/đã duyệt đạt.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class AvailableScriptsTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task DemDungKhoTheoChuDe()
    {
        var ct = TestContext.Current.CancellationToken;

        var before = await WaveService.GetAvailableScriptsAsync("ItTechnology", ct);

        Assert.Equal("ItTechnology", before.Domain);
        Assert.False(string.IsNullOrWhiteSpace(before.TopicName));
        Assert.Equal(before.ValidatedTotal, before.RecordedPairs + before.AvailablePairs);

        var suffix = Guid.NewGuid().ToString("N");
        var script = await CreateValidatedScriptAsync(
            $"[vi]Kiểm thử [en]cache {suffix}", $"[vi]Kiểm thử bộ nhớ đệm {suffix}",
            "ItTechnology", AdminUserId);
        await CreateRecordingAsync(script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);

        var after = await WaveService.GetAvailableScriptsAsync("ItTechnology", ct);

        Assert.Equal(before.ValidatedTotal + 1, after.ValidatedTotal);
        Assert.Equal(before.RecordedPairs + 1, after.RecordedPairs);
        Assert.Equal(before.AvailablePairs, after.AvailablePairs);
    }
}
