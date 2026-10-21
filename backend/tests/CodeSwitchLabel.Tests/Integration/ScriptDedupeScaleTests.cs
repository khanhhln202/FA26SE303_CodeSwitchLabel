using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Dedupe khi nhập câu: so theo khoá chuẩn hoá (bỏ nhãn, gộp khoảng trắng, hạ chữ thường)
/// và batch 10.000 dòng vẫn tra được câu nằm ở cuối danh sách.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ScriptDedupeScaleTests : IntegrationTestBase
{
    public ScriptDedupeScaleTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task FindExisting_10kDong_KhopDuocOCuoiDanhSach()
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
        Assert.Contains(CodeSwitchText.ComparisonKey(needle), found);
    }

    [Fact]
    public async Task FindExisting_KhacHoaThuongVaKhoangTrang_VanKhop()
    {
        var ct = TestContext.Current.CancellationToken;
        var stored = "[vi]Em nên [en]scan [vi]tài liệu này.";
        await CreateScriptAsync(
            stored, "[vi]Em nên quét tài liệu này.",
            ScriptDomain.ItTechnology, AdminUserId);

        var found = await Scripts.FindExistingContentsAsync(
            ["  [vi]EM NÊN [en]SCAN [vi]TÀI   LIỆU NÀY.  "], ct);

        var key = Assert.Single(found);
        Assert.Equal(CodeSwitchText.ComparisonKey(stored), key);
    }
}
