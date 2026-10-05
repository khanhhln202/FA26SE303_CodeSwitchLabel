using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Luật của chiến dịch: ai tạo, giao cho ai, và khi nào thì còn nhận thêm task.
/// Mỗi test tự tạo chiến dịch riêng để không phụ thuộc dữ liệu của test khác.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class CampaignRuleTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    public async Task ChienDichDaDong_KhongNhanThemTask(string status)
    {
        var admin = await GetAdminTokenAsync();
        var manager = await GetManagerTokenAsync();
        var campaignId = await TaoChienDichDaGiaoAsync(admin, await LayUserIdAsync(manager));

        var doiTrangThai = await PatchAsync($"/api/campaigns/{campaignId}", new { status }, admin);
        Assert.Equal(HttpStatusCode.OK, doiTrangThai.StatusCode);

        var taoTask = await PostAsync("/api/tasks", TaskMoi(campaignId), manager);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, taoTask.StatusCode);
        Assert.Equal("campaign_not_accepting_tasks", await LayMaLoiAsync(taoTask));
    }

    [Fact]
    public async Task ChienDichDangMo_VanNhanTask()
    {
        var admin = await GetAdminTokenAsync();
        var manager = await GetManagerTokenAsync();
        var campaignId = await TaoChienDichDaGiaoAsync(admin, await LayUserIdAsync(manager));

        var moChienDich = await PatchAsync($"/api/campaigns/{campaignId}", new { status = "Open" }, admin);
        Assert.Equal(HttpStatusCode.OK, moChienDich.StatusCode);

        var taoTask = await PostAsync("/api/tasks", TaskMoi(campaignId), manager);

        Assert.Equal(HttpStatusCode.Created, taoTask.StatusCode);
    }

    /// <summary>Thông báo lỗi phải là tiếng Việt như mọi lỗi nghiệp vụ khác, vì frontend hiện thẳng cho người dùng.</summary>
    [Fact]
    public async Task GiaoChienDichChoNguoiKhongPhaiTaskManager_BaoLoiTiengViet()
    {
        var admin = await GetAdminTokenAsync();
        var speaker = await GetSpeakerTokenAsync();
        var campaignId = await TaoChienDichAsync(admin);

        var giao = await PostAsync($"/api/campaigns/{campaignId}/assign",
            new { assignedToUserId = await LayUserIdAsync(speaker) }, admin);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, giao.StatusCode);

        using var doc = JsonDocument.Parse(await giao.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("assigned_to_invalid_role", doc.RootElement.GetProperty("code").GetString());
        Assert.Contains("Task Manager", doc.RootElement.GetProperty("title").GetString()!);
    }

    // ----------------------------------------------------------------- dựng dữ liệu

    private static object TaskMoi(long campaignId) => new
    {
        campaignId,
        taskType = "Recording",
        description = "Task kiểm thử luật chiến dịch",
        targetQty = 1,
        deadline = DateTimeOffset.UtcNow.AddDays(7)
    };

    private async Task<long> TaoChienDichAsync(string adminToken)
    {
        var response = await PostAsync("/api/campaigns", new
        {
            campaignName = $"Luật chiến dịch {Guid.NewGuid():N}",
            targetQty = 2000,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        }, adminToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.GetProperty("campaignId").GetInt64();
    }

    private async Task<long> TaoChienDichDaGiaoAsync(string adminToken, long managerId)
    {
        var campaignId = await TaoChienDichAsync(adminToken);

        var giao = await PostAsync($"/api/campaigns/{campaignId}/assign",
            new { assignedToUserId = managerId }, adminToken);

        Assert.Equal(HttpStatusCode.OK, giao.StatusCode);
        return campaignId;
    }

    private async Task<long> LayUserIdAsync(string accessToken)
    {
        using var client = CreateAuthenticatedClient(accessToken);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", JsonOptions, TestContext.Current.CancellationToken);
        return me.GetProperty("userId").GetInt64();
    }

    private static async Task<string?> LayMaLoiAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
