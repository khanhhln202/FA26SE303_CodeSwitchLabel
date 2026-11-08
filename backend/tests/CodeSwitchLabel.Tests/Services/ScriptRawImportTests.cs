using System.Text;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Services.WorkTasks;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>Import .txt: LLM gán nhãn từng cặp, cặp hỏng bị bỏ qua, phần còn lại vào đúng luồng import.</summary>
[Trait("Category", "Unit")]
public sealed class ScriptRawImportTests
{
    [Fact]
    public async Task ImportRaw_HaiCapHopLe_NhapCaHai()
    {
        var (service, repo, labeling) = CreateService();
        var added = CaptureAdded(repo);

        labeling.Setup(l => l.AutoLabelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string cs, string vi, CancellationToken _) => new LabeledPair(
                "[vi]" + cs.Replace("scan ", "[en]scan [vi]"),
                [new AlignmentItem { Source = "scan", Target = "quét" }]));

        using var stream = Of("""
            Em nên scan tài liệu này.
            Em nên quét tài liệu này.
            Em nên scan hợp đồng.
            Em nên quét hợp đồng.
            """);

        var result = await service.ImportRawAsync(
            stream, "raw.txt", importedById: 1, domain: "IT/Technology",
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Imported);
        Assert.Empty(result.Skipped);
        Assert.Equal(2, added.Count);
        Assert.Equal("[vi]Em nên [en]scan [vi]tài liệu này.", added[0].CsContent);
        Assert.Equal("[vi]Em nên quét tài liệu này.", added[0].ViContent);
    }

    [Fact]
    public async Task ImportRaw_MotCapLoiLlm_VanNhapCapConLai()
    {
        var (service, repo, labeling) = CreateService();
        var added = CaptureAdded(repo);

        labeling.SetupSequence(l => l.AutoLabelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnprocessableException("llm_parse_error", "LLM trả về sai."))
            .ReturnsAsync(new LabeledPair(
                "[vi]Em nên [en]scan [vi]hợp đồng.",
                [new AlignmentItem { Source = "scan", Target = "quét" }]));

        using var stream = Of("""
            Em nên scan tài liệu này.
            Em nên quét tài liệu này.
            Em nên scan hợp đồng.
            Em nên quét hợp đồng.
            """);

        var result = await service.ImportRawAsync(
            stream, "raw.txt", importedById: 1, domain: "IT/Technology",
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Imported);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("pair-1", skipped.Id);
        Assert.Equal("llm_parse_error", skipped.Code);
        var only = Assert.Single(added);
        Assert.Equal("[vi]Em nên [en]scan [vi]hợp đồng.", only.CsContent);
    }

    [Fact]
    public async Task ImportRaw_KhongCoLlm_TuGanNhanVaLayNghiaTheoViTri()
    {
        var (service, repo, labeling) = CreateService(configured: false);
        var added = CaptureAdded(repo);
        var words = CaptureAddedWords(repo);

        labeling.Setup(l => l.AutoLabelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string cs, string vi, CancellationToken _) => RuleBasedLabeler.Label(cs, vi));

        using var stream = Of("""
            Em nên scan tài liệu này.
            Em nên quét tài liệu này.
            """);

        var result = await service.ImportRawAsync(
            stream, "raw.txt", importedById: 1, domain: "IT/Technology",
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Imported);
        Assert.Empty(result.Skipped);

        var script = Assert.Single(added);
        Assert.Equal("[vi]Em nên [en]scan [vi]tài liệu này.", script.CsContent);

        var word = Assert.Single(words);
        Assert.Equal("scan", word.EnWord);
        Assert.Equal("quét", word.ViWord);

        labeling.Verify(l => l.AutoLabelAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportRaw_ThieuChuDe_ThiBaoLoiTruocKhiGoiLlm()
    {
        var (service, _, labeling) = CreateService();

        using var stream = Of("Em nên scan tài liệu này.\nEm nên quét tài liệu này.");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() => service.ImportRawAsync(
            stream, "raw.txt", importedById: 1, domain: " ",
            TestContext.Current.CancellationToken));

        Assert.Equal("domain_required", ex.Code);
        labeling.Verify(l => l.AutoLabelAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (
        ScriptService Service,
        Mock<IScriptRepository> Repository,
        Mock<ILabelingService> Labeling) CreateService(bool configured = true)
    {
        var repo = new Mock<IScriptRepository>();

        var transaction = new Mock<IDbContextTransaction>();
        transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        repo.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        repo.Setup(r => r.FindExistingContentsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));

        var nextId = 1;
        repo.Setup(r => r.GenerateIdAsync(
                It.IsAny<int>(), It.IsAny<ScriptDomain>(), It.IsAny<ScriptRelation>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"s_11100000{nextId++}");

        var labeling = new Mock<ILabelingService>();
        labeling.SetupGet(l => l.IsConfigured).Returns(configured);

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
            labeling.Object,
            TimeProvider.System,
            Mock.Of<ILogger<ScriptService>>());

        return (service, repo, labeling);
    }

    private static List<Script> CaptureAdded(Mock<IScriptRepository> repo)
    {
        var added = new List<Script>();
        repo.Setup(r => r.Add(It.IsAny<Script>())).Callback<Script>(added.Add);
        return added;
    }

    private static List<ScriptWord> CaptureAddedWords(Mock<IScriptRepository> repo)
    {
        var added = new List<ScriptWord>();
        repo.Setup(r => r.AddWords(It.IsAny<IEnumerable<ScriptWord>>()))
            .Callback<IEnumerable<ScriptWord>>(added.AddRange);
        return added;
    }

    private static MemoryStream Of(string text) =>
        new(Encoding.UTF8.GetBytes(text));
}
