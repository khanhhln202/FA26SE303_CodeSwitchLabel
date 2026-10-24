using CodeSwitchLabel.Repositories.Entities;
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
        var result = await Scripts.GetAsync(script.ScriptId, TestContext.Current.CancellationToken);

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
        var result = await Scripts.GetForUpdateAsync(script.ScriptId, TestContext.Current.CancellationToken);

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
        var result = await Scripts.GetWithReviewsAsync(script.ScriptId, TestContext.Current.CancellationToken);

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
        var (items, total) = await Scripts.SearchAsync(null, null, "test", 1, 10, default, TestContext.Current.CancellationToken);

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
        var exists = await Scripts.ContentExistsAsync("[vi]Unique [en]content [vi]here", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ContentExistsAsync_IgnoresCaseAndWhitespace()
    {
        // Arrange
        await CreateScriptAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này.",
            "[vi]Em nên quét tài liệu này.",
            ScriptDomain.ItTechnology,
            AdminUserId);

        // Act — khác hoa/thường và gộp nhiều khoảng trắng.
        var exists = await Scripts.ContentExistsAsync(
            "  [vi]EM NÊN [en]SCAN [vi]TÀI   LIỆU NÀY.  ",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ContentExistsAsync_ReturnsFalse_WhenContentDoesNotExist()
    {
        // Act
        var exists = await Scripts.ContentExistsAsync("[vi]Non [en]existent [vi]content", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(exists);
    }

    // ---------------------------------------------------------------- sắp xếp

    /// <summary>
    /// Ba câu của riêng test này (kèm từ khoá chỉ chúng có) và mốc thời gian cách nhau rõ.
    /// Mốc đặt tay chứ không dựa vào thứ tự tạo: UtcNow trên Windows chỉ chính xác tới 15ms
    /// nên hai câu tạo liền có thể trùng mốc, mà trùng mốc thì test sắp xếp không còn ý nghĩa.
    /// </summary>
    private async Task<(string Keyword, Script[] Scripts)> CreateThreeSortedScriptsAsync()
    {
        var token = Guid.NewGuid().ToString("N")[..10];
        var baseTime = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

        var scripts = new List<Script>();
        for (var i = 0; i < 3; i++)
        {
            var script = await CreateScriptAsync(
                $"[vi]Cau [en]xep {token} so {i}", $"[vi]Câu sắp xếp {token} số {i}",
                ScriptDomain.ItTechnology, AdminUserId);

            script.CreatedAt = baseTime.AddHours(i);
            scripts.Add(script);
        }

        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (token, [.. scripts]);
    }

    [Fact]
    public async Task SearchAsync_KhongTruyenSort_GiuNguyenThuTuMoiNhatTruoc()
    {
        // Regression client cũ không gửi sort thì phải y hệt trước khi có tính năng này.
        var (keyword, scripts) = await CreateThreeSortedScriptsAsync();

        var (items, total) = await Scripts.SearchAsync(
            null, null, keyword, 1, 10, default, TestContext.Current.CancellationToken);

        Assert.Equal(3, total);
        Assert.Equal(
            [scripts[2].ScriptId, scripts[1].ScriptId, scripts[0].ScriptId],
            items.Select(s => s.ScriptId));
    }

    [Fact]
    public async Task SearchAsync_SapXepTheoCreatedAt_TangDanVaGiamDan()
    {
        var (keyword, scripts) = await CreateThreeSortedScriptsAsync();

        var (asc, _) = await Scripts.SearchAsync(
            null, null, keyword, 1, 10,
            SortSpec<ScriptSortField>.From("createdAt", "asc"), TestContext.Current.CancellationToken);

        var (desc, _) = await Scripts.SearchAsync(
            null, null, keyword, 1, 10,
            SortSpec<ScriptSortField>.From("createdAt", "desc"), TestContext.Current.CancellationToken);

        Assert.Equal([scripts[0].ScriptId, scripts[1].ScriptId, scripts[2].ScriptId], asc.Select(s => s.ScriptId));
        Assert.Equal([scripts[2].ScriptId, scripts[1].ScriptId, scripts[0].ScriptId], desc.Select(s => s.ScriptId));
    }

    [Fact]
    public async Task SearchAsync_SapXepTheoScriptId_TheoChuoiSo()
    {
        var (keyword, scripts) = await CreateThreeSortedScriptsAsync();

        var (items, _) = await Scripts.SearchAsync(
            null, null, keyword, 1, 10,
            SortSpec<ScriptSortField>.From("scriptId", "asc"), TestContext.Current.CancellationToken);

        Assert.Equal(
            [.. scripts.OrderBy(s => s.ScriptId).Select(s => s.ScriptId)],
            items.Select(s => s.ScriptId));
    }

    [Fact]
    public async Task SearchAsync_TenCotSao_RaKetQuaGiongKhongTruyenSort()
    {
        var (keyword, _) = await CreateThreeSortedScriptsAsync();

        var (sai, _) = await Scripts.SearchAsync(
            null, null, keyword, 1, 10,
            SortSpec<ScriptSortField>.From("khongCoCotNay", "asc"), TestContext.Current.CancellationToken);

        var (macDinh, _) = await Scripts.SearchAsync(
            null, null, keyword, 1, 10, default, TestContext.Current.CancellationToken);

        Assert.Equal(macDinh.Select(s => s.ScriptId), sai.Select(s => s.ScriptId));
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
        var hasRecordings = await Scripts.HasRecordingsAsync(script.ScriptId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(hasRecordings);
    }
}
