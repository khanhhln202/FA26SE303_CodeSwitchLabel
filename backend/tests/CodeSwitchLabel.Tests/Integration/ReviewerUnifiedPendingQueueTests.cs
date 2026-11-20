using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ReviewerUnifiedPendingQueueTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task HienThiCaDongGopVaBaoLoi()
    {
        var ct = TestContext.Current.CancellationToken;
        var contribution = await ScriptService.ContributeAsync(new CreateScriptRequest
        {
            CsContent = "[vi]Đang [en]debug [vi]lỗi này.",
            ViContent = "[vi]Đang gỡ lỗi này.",
            Domain = "ItTechnology",
            Alignment = [new AlignmentItem { Source = "debug", Target = "gỡ lỗi" }],
            Comment = "Ví dụ đóng góp"
        }, SpeakerUserId, ct);
        var reportTarget = await CreateValidatedScriptAsync(
            "[vi]Mở [en]browser [vi]để xem.", "[vi]Mở trình duyệt để xem.",
            "ItTechnology", SpeakerUserId);
        await ProposalService.CreateUnifiedAsync(new ContributeOrReportRequest(
            "report", null, null, null, null, null, reportTarget.ScriptId, "other", "Sai nội dung"),
            SpeakerUserId, ct);

        var queue = Services.GetRequiredService<IScriptQueueService>();
        var result = await queue.ListUnifiedPendingAsync(ReviewerUserId, "all", null, 1, 20, ct);

        Assert.Contains(result.Items, item => item.ScriptId == contribution.ScriptId && item.Type == "contribution");
        Assert.Contains(result.Items, item => item.ScriptId == reportTarget.ScriptId && item.Type == "report");
        Assert.Equal("Ví dụ đóng góp", result.Items.Single(item => item.ScriptId == contribution.ScriptId).Comment);
    }
}
