using System.Text;
using System.Text.Json;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Dedupe của import: trim hai đầu rồi so khớp CHÍNH XÁC, không chuẩn hoá gì thêm.
/// Batch 10.000 dòng phải gom về đúng một nội dung để tra kho, không gọi DB 10.000 lần.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ScriptImportDedupeTests
{
    [Fact]
    public async Task Import_10kDong_TrimRoiSoKhopChinhXac()
    {
        const string stored = "[vi]Nội dung trùng [en]dedupe [vi]mười nghìn dòng.";

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
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { stored });

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
            // Cùng nội dung lõi, khác khoảng trắng hai đầu.
            cs_transcript = $"  {stored}\t",
            vi_equivalent = $"[vi]Nội dung trùng mười nghìn dòng {i}."
        });
        var json = JsonSerializer.Serialize(rows);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await service.ImportAsync(
            stream, "dedupe-10k.json", importedById: 1, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Imported);
        Assert.Equal(10_000, result.Skipped.Count);
        Assert.All(result.Skipped, s => Assert.Equal("duplicate_content", s.Code));

        // 10.000 dòng khác khoảng trắng hai đầu chỉ còn MỘT giá trị đã trim.
        Assert.NotNull(queried);
        Assert.Single(queried!);
        Assert.Equal(stored, queried![0]);

        transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
