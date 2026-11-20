using System.Net;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ScriptUnifiedContributionApiTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task SubmitAcceptsContributionAndReport()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await GetSpeakerTokenAsync();
        var admin = await GetAdminTokenAsync();

        using var contributionResponse = await PostAsync("/api/speaker/scripts/submit", new
        {
            type = "contribution",
            csContent = "[vi]Mở [en]browser [vi]này.",
            viContent = "[vi]Mở trình duyệt này.",
            domain = "ItTechnology",
            comment = "Ví dụ do Speaker gửi",
            alignment = new[] { new { source = "browser", target = "trình duyệt", relation = "semantic_equivalent" } }
        }, speaker);
        Assert.Equal(HttpStatusCode.OK, contributionResponse.StatusCode);
        using var contribution = JsonDocument.Parse(await contributionResponse.Content.ReadAsStringAsync(ct));
        Assert.Equal("contribution", contribution.RootElement.GetProperty("type").GetString());

        using var createResponse = await PostAsync("/api/scripts", new
        {
            csContent = "[vi]Gửi [en]email [vi]đi.",
            viContent = "[vi]Gửi thư điện tử đi.",
            domain = "ItTechnology",
            alignment = new[] { new { source = "email", target = "thư điện tử", relation = "semantic_equivalent" } }
        }, admin);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(ct));
        var scriptId = created.RootElement.GetProperty("scriptId").GetString();

        using var reportResponse = await PostAsync("/api/speaker/scripts/submit", new
        {
            type = "report",
            scriptId,
            errorReasonCode = "other",
            comment = "Câu này cần kiểm tra lại"
        }, speaker);
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        using var report = JsonDocument.Parse(await reportResponse.Content.ReadAsStringAsync(ct));
        Assert.Equal("report", report.RootElement.GetProperty("type").GetString());
        Assert.Equal(scriptId, report.RootElement.GetProperty("scriptId").GetString());
    }
}
