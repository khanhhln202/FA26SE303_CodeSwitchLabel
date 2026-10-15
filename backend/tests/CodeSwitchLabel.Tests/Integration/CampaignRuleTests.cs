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
    /// <summary>
    /// TEAM_013: đợt mới tạo chưa có nhiệm vụ nào thì là Open, và nhận task bình thường.
    /// Trạng thái không cần mở bằng tay nữa — nó tự nhảy theo số nhiệm vụ bên trong.
    /// </summary>
    [Fact]
    public async Task ChienDichMoiTao_LaDangMoVaVanNhanTask()
    {
        var admin = await GetAdminTokenAsync();
        var manager = await GetManagerTokenAsync();
        var campaignId = await TaoChienDichDaGiaoAsync(admin, await LayUserIdAsync(manager));

        var taoTask = await PostAsync("/api/tasks", TaskMoi(campaignId), manager);

        Assert.Equal(HttpStatusCode.Created, taoTask.StatusCode);
    }

    /// <summary>
    /// TEAM_013: bỏ luật "đợt đã đóng thì không nhận thêm task". Gắn nhiệm vụ mới vào đợt
    /// đã hoàn thành thì đợt phải mở lại chứ không phải từ chối — nếu chặn thì đợt xong
    /// là không bao giờ làm tiếp được nữa.
    /// </summary>
    [Fact]
    public async Task ChienDichDaHoanThanh_VanNhanTaskVaTuMoLai()
    {
        var admin = await GetAdminTokenAsync();
        var manager = await GetManagerTokenAsync();
        var campaignId = await TaoChienDichDaGiaoAsync(admin, await LayUserIdAsync(manager));

        var taoTask = await PostAsync("/api/tasks", TaskMoi(campaignId), manager);
        Assert.Equal(HttpStatusCode.Created, taoTask.StatusCode);

        // Chốt nhiệm vụ vừa tạo → đợt không còn việc nào chưa xong nên thành Completed.
        var taskId = (await DocJsonAsync(taoTask)).GetProperty("summary").GetProperty("taskId").GetInt64();
        var chotXong = await PatchAsync($"/api/tasks/{taskId}", new { status = "Completed" }, manager);
        Assert.Equal(HttpStatusCode.OK, chotXong.StatusCode);
        Assert.Equal("Completed", (await DocJsonAsync(chotXong)).GetProperty("summary").GetProperty("status").GetString());

        Assert.Equal("Completed", (await DocJsonAsync(await GetAsync($"/api/campaigns/{campaignId}", admin)))
            .GetProperty("status").GetString());

        // Gắn nhiệm vụ thứ hai → đợt mở lại.
        var taoTask2 = await PostAsync("/api/tasks", TaskMoi(campaignId), manager);
        Assert.Equal(HttpStatusCode.Created, taoTask2.StatusCode);

        Assert.Equal("InProgress", (await DocJsonAsync(await GetAsync($"/api/campaigns/{campaignId}", admin)))
            .GetProperty("status").GetString());
    }

    /// <summary>Trường `status` không còn trong body của PATCH chiến dịch — bỏ trống là giữ nguyên.</summary>
    [Fact]
    public async Task SuaChienDichKhongConTruongStatus()
    {
        var admin = await GetAdminTokenAsync();
        var campaignId = await TaoChienDichAsync(admin);
        var hienTai = await DocJsonAsync(await GetAsync($"/api/campaigns/{campaignId}", admin));

        var sua = await PatchAsync($"/api/campaigns/{campaignId}",
            new { status = "Completed", campaignName = "Đợt đã đổi tên" }, admin);

        Assert.Equal(HttpStatusCode.OK, sua.StatusCode);

        var sau = await DocJsonAsync(sua);
        Assert.Equal("Đợt đã đổi tên", sau.GetProperty("campaignName").GetString());
        Assert.Equal(hienTai.GetProperty("status").GetString(), sau.GetProperty("status").GetString());
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
            domain = "ItTechnology",
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

    /// <summary>Đọc thẳng gốc JSON: các test dưới đây chỉ cần một trường, không cần bản ghi kiểu C#.</summary>
    private static async Task<JsonElement> DocJsonAsync(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.Clone();
    }
}
