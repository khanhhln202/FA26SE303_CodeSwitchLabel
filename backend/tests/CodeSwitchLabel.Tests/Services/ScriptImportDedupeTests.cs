using System.Text;
using System.Text.Json;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Dedupe của import: bỏ nhãn ngôn ngữ, gộp khoảng trắng, hạ chữ thường rồi so khớp.
/// Batch 10.000 dòng phải gom về đúng một khoá để tra kho, không gọi DB 10.000 lần.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ScriptImportDedupeTests
{
    [Fact]
    public async Task Import_10kDong_ChuanHoaRoiSoKhop()
    {
        const string stored = "[vi]Nội dung trùng [en]dedupe [vi]mười nghìn dòng.";
        var storedKey = CodeSwitchText.ComparisonKey(stored);

        var repo = new Mock<IScriptRepository>();
        var transaction = new Mock<IDbContextTransaction>();
        transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        repo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        string[]? queried = null;
        repo.Setup(r => r.FindExistingContentsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, CancellationToken>((contents, _) =>
                queried = contents.ToArray())
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { storedKey });

        var config = new Mock<ISystemConfigService>();
        config.Setup(c => c.GetIntAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100_000);

        var service = new ScriptService(
            repo.Object,
            Mock.Of<IScriptQueueService>(),
            Mock.Of<IReasonRepository>(),
            Mock.Of<IUserDomainRepository>(),
            config.Object,
            Mock.Of<ITaskProgressTracker>(),
            TimeProvider.System,
            Mock.Of<ILogger<ScriptService>>());

        var rows = Enumerable.Range(0, 10_000).Select(i => new
        {
            id = i.ToString(),
            domain = "IT/Technology",
            // Cùng nội dung lõi; lẻ thì đổi hoa thường, chẵn thì đổi khoảng trắng.
            cs_transcript = i % 2 == 0
                ? $"  {stored}\t"
                : $" {stored.Replace("Nội dung", "NỘI DUNG").Replace("dedupe", "DEDUPE")}  ",
            vi_equivalent = $"[vi]Nội dung trùng mười nghìn dòng {i}."
        });
        var json = JsonSerializer.Serialize(rows);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await service.ImportAsync(
            stream, "dedupe-10k.json", importedById: 1, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Imported);
        Assert.Equal(10_000, result.Skipped.Count);
        Assert.All(result.Skipped, s => Assert.Equal("duplicate_content", s.Code));

        // 10.000 dòng khác khoảng trắng hai đầu chỉ còn MỘT khoá chuẩn hoá.
        Assert.NotNull(queried);
        Assert.Single(queried!);
        Assert.Equal(storedKey, queried![0]);

        transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Import_TrongCungBatch_KhacHoaThuongVaKhoangTrang_ChiNhapMot()
    {
        const string first = "[vi]Em nên [en]scan [vi]tài liệu này.";
        var rows = new[]
        {
            new
            {
                id = "1",
                domain = "IT/Technology",
                cs_transcript = first,
                vi_equivalent = "[vi]Em nên quét tài liệu này.",
                alignment = new[] { new { source = "scan", target = "quét" } }
            },
            new
            {
                id = "2",
                domain = "IT/Technology",
                cs_transcript = "  [vi]EM NÊN [en]SCAN [vi]TÀI LIỆU NÀY.  ",
                vi_equivalent = "[vi]Em nên quét tài liệu này.",
                alignment = new[] { new { source = "scan", target = "quét" } }
            },
            new
            {
                id = "3",
                domain = "IT/Technology",
                cs_transcript = "[vi]Em  nên [en]scan [vi]tài   liệu này.",
                vi_equivalent = "[vi]Em nên quét tài liệu này.",
                alignment = new[] { new { source = "scan", target = "quét" } }
            }
        };

        var repo = new Mock<IScriptRepository>();
        var transaction = new Mock<IDbContextTransaction>();
        transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        repo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        repo.Setup(r => r.GenerateIdAsync(
                It.IsAny<int>(), It.IsAny<ScriptDomain>(), It.IsAny<ScriptRelation>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("s_111000001");
        repo.Setup(r => r.FindExistingContentsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));

        var config = new Mock<ISystemConfigService>();
        config.Setup(c => c.GetIntAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100_000);

        var service = new ScriptService(
            repo.Object,
            Mock.Of<IScriptQueueService>(),
            Mock.Of<IReasonRepository>(),
            Mock.Of<IUserDomainRepository>(),
            config.Object,
            Mock.Of<ITaskProgressTracker>(),
            TimeProvider.System,
            Mock.Of<ILogger<ScriptService>>());

        var json = JsonSerializer.Serialize(rows);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await service.ImportAsync(
            stream, "same-batch.json", importedById: 1, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Imported);
        Assert.Equal(2, result.Skipped.Count);
        Assert.All(result.Skipped, s => Assert.Equal("duplicate_content", s.Code));
        Assert.Equal("s_111000001", Assert.Single(result.ScriptIds));
    }
}
