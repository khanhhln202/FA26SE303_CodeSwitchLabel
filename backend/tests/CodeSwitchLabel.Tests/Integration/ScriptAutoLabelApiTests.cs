using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Auto-label đứng riêng và đóng góp câu thô: chưa cấu hình LLM thì dùng luật,
/// alignment do backend sinh chứ không lấy từ client.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ScriptAutoLabelApiTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task AutoLabel_TraVeCauDaGanNhanVaAlignment()
    {
        var admin = await GetAdminTokenAsync();
        var suffix = Random.Shared.Next(100_000, 999_999);

        var response = await PostAsync("/api/scripts/auto-label", new
        {
            csRaw = $"Em nên scan tài liệu số {suffix} này.",
            viRaw = $"Em nên quét tài liệu số {suffix} này."
        }, admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal($"[vi]Em nên [en]scan [vi]tài liệu số {suffix} này.",
            body.GetProperty("csTagged").GetString());

        var item = body.GetProperty("alignment").EnumerateArray().Single();
        Assert.Equal("scan", item.GetProperty("source").GetString());
        Assert.Equal("quét", item.GetProperty("target").GetString());
    }

    [Fact]
    public async Task AutoLabel_Speaker_Tra403()
    {
        var speaker = await GetSpeakerTokenAsync();

        var response = await PostAsync("/api/scripts/auto-label", new
        {
            csRaw = "Em nên scan tài liệu này.",
            viRaw = "Em nên quét tài liệu này."
        }, speaker);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Contribute_CauTho_KhongCanAlignment_TuGanNhan()
    {
        var speaker = await GetSpeakerTokenAsync();
        var suffix = Random.Shared.Next(100_000, 999_999);

        var response = await PostAsync("/api/speaker/scripts/contribute", new
        {
            csRaw = $"Em nên scan tài liệu số {suffix} này.",
            viRaw = $"Em nên quét tài liệu số {suffix} này.",
            // Gửi kèm cặp đã gán nhãn để chứng minh dữ liệu thô thắng và alignment client bị bỏ qua.
            csContent = "[vi]Sai hoàn toàn.",
            viContent = "[vi]Sai hoàn toàn.",
            domain = "ItTechnology",
            alignment = new[]
            {
                new { source = "wrong", source_lang = "en", target = "sai", target_lang = "vi", relation = "semantic_equivalent" }
            }
        }, speaker);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions, TestContext.Current.CancellationToken);

        Assert.Equal("PendingValidation", body.GetProperty("status").GetString());
        Assert.Equal($"[vi]Em nên [en]scan [vi]tài liệu số {suffix} này.",
            body.GetProperty("csContent").GetString());
        Assert.Equal($"[vi]Em nên quét tài liệu số {suffix} này.",
            body.GetProperty("viContent").GetString());

        var item = body.GetProperty("alignment").EnumerateArray().Single();
        Assert.Equal("scan", item.GetProperty("source").GetString());
        Assert.Equal("quét", item.GetProperty("target").GetString());
    }

    [Fact]
    public async Task Admin_CauTho_ThieuNghia_KhongDuocVaoThangDaDuyet()
    {
        var admin = await GetAdminTokenAsync();
        var suffix = Random.Shared.Next(100_000, 999_999);

        // "code<số>" là một từ tiếng Anh nhưng câu Việt đã hết từ tương ứng,
        // nên luật không sinh được nghĩa. Admin tạo câu vào thẳng Validated,
        // không có bước duyệt nào điền nghĩa hộ.
        var response = await PostAsync("/api/scripts", new
        {
            csRaw = $"Em ăn cơm rồi code{suffix}.",
            viRaw = "Em ăn cơm rồi.",
            domain = "DailyLife"
        }, admin);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("alignment_word_missing", body.GetProperty("code").GetString());
    }
}
