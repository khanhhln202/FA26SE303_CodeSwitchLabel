using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Dedupe khi nhập câu so khớp CHÍNH XÁC nội dung sau khi trim, và batch 10.000 dòng
/// phải đi qua đủ các chunk truy vấn của repository.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ScriptDedupeScaleTests : IntegrationTestBase
{
    public ScriptDedupeScaleTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task FindExisting_10kDong_KhopDuocOMChunkCuoi()
    {
        var ct = TestContext.Current.CancellationToken;
        var needle = "[vi]Câu cần tìm [en]lastchunk [vi]ở cuối danh sách.";
        await CreateScriptAsync(
            needle, "[vi]Câu cần tìm ở cuối danh sách.",
            ScriptDomain.Education, AdminUserId);

        var contents = new List<string>(10_000);
        for (var i = 0; i < 9_999; i++)
        {
            contents.Add($"[vi]Câu không tồn tại {i} [en]missing [vi]trong kho.");
        }
        contents.Add(needle);

        var found = await Scripts.FindExistingContentsAsync(contents, ct);

        Assert.Single(found);
        Assert.Contains(needle, found);
    }
}
