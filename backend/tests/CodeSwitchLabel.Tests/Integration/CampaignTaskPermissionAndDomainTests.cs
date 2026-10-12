using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// TEAM_003: Kiểm thử toàn diện quyền Task Manager, xoá đợt, xoá nhiệm vụ và ràng buộc chủ đề (domain).
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class CampaignTaskPermissionAndDomainTests(DatabaseFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task TaskManager_TaoChienDich_TuDongGanChoChinhMinh()
    {
        var managerToken = await GetManagerTokenAsync();
        var managerId = await LayUserIdAsync(managerToken);

        var response = await PostAsync("/api/campaigns", new
        {
            campaignName = $"Đợt của TaskManager {Guid.NewGuid():N}",
            domain = "ItTechnology",
            targetQty = 2000,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        }, managerToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var created = doc.RootElement;
        Assert.Equal(managerId, created.GetProperty("assignedTo").GetInt64());
        Assert.Equal(managerId, created.GetProperty("createdBy").GetInt64());
        Assert.Equal("ItTechnology", created.GetProperty("domain").GetString());
    }

    [Fact]
    public async Task TaskManager_TaoTaskTrongChienDichChinhMinh_ThanhCong()
    {
        var managerToken = await GetManagerTokenAsync();
        var campaignId = await TaoChienDichBoiManagerAsync(managerToken, "ItTechnology");

        var response = await PostAsync("/api/tasks", new
        {
            campaignId,
            taskType = "Recording",
            domain = "ItTechnology",
            description = "Task trong đợt do chính manager tạo",
            targetQty = 10,
            deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, managerToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TaskManager_SuaVaXoaChienDichCuaChinhMinh_ThanhCong()
    {
        var managerToken = await GetManagerTokenAsync();
        var campaignId = await TaoChienDichBoiManagerAsync(managerToken, "ItTechnology");

        // 1. Sửa
        var patchRes = await PatchAsync($"/api/campaigns/{campaignId}", new
        {
            campaignName = "Tên đợt đã đổi"
        }, managerToken);
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        // 2. Xoá
        var deleteRes = await DeleteAsync($"/api/campaigns/{campaignId}", managerToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // 3. Không còn tìm thấy
        var getRes = await GetAsync($"/api/campaigns/{campaignId}", managerToken);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task Admin_TaoChienDich_ChuaGanChoAi()
    {
        var adminToken = await GetAdminTokenAsync();

        var response = await PostAsync("/api/campaigns", new
        {
            campaignName = $"Đợt do Admin tạo {Guid.NewGuid():N}",
            domain = "Education",
            targetQty = 2000,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        }, adminToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(doc.RootElement.GetProperty("assignedTo").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task TaskManager_KhongTheSuaHoacXoaChienDichCuaNguoiKhac()
    {
        var adminToken = await GetAdminTokenAsync();
        var managerToken = await GetManagerTokenAsync();

        // Admin tạo chiến dịch và chưa giao cho manager này
        var response = await PostAsync("/api/campaigns", new
        {
            campaignName = $"Đợt của Admin {Guid.NewGuid():N}",
            domain = "ItTechnology",
            targetQty = 2000,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        }, adminToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var campaignId = doc.RootElement.GetProperty("campaignId").GetInt64();

        // Manager cố sửa -> 403
        var patchRes = await PatchAsync($"/api/campaigns/{campaignId}", new
        {
            campaignName = "Manager cố sửa đợt của Admin"
        }, managerToken);
        Assert.Equal(HttpStatusCode.Forbidden, patchRes.StatusCode);

        // Manager cố xoá -> 403
        var deleteRes = await DeleteAsync($"/api/campaigns/{campaignId}", managerToken);
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);
    }

    [Fact]
    public async Task XoaChienDich_TaskConTuDongThanhDocLap()
    {
        var managerToken = await GetManagerTokenAsync();
        var campaignId = await TaoChienDichBoiManagerAsync(managerToken, "ItTechnology");

        // Tạo task trong đợt
        var taskRes = await PostAsync("/api/tasks", new
        {
            campaignId,
            taskType = "Recording",
            domain = "ItTechnology",
            description = "Task sẽ thành độc lập sau khi đợt bị xoá",
            targetQty = 10,
            deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, managerToken);
        Assert.Equal(HttpStatusCode.Created, taskRes.StatusCode);

        using var taskDoc = JsonDocument.Parse(await taskRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var taskId = taskDoc.RootElement.GetProperty("summary").GetProperty("taskId").GetInt64();

        // Xoá đợt
        var delCampRes = await DeleteAsync($"/api/campaigns/{campaignId}", managerToken);
        Assert.Equal(HttpStatusCode.NoContent, delCampRes.StatusCode);

        // Kiểm tra task con: vẫn còn và campaignId == null!
        var getTaskRes = await GetAsync($"/api/tasks/{taskId}", managerToken);
        Assert.Equal(HttpStatusCode.OK, getTaskRes.StatusCode);

        using var getTaskDoc = JsonDocument.Parse(await getTaskRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var summary = getTaskDoc.RootElement.GetProperty("summary");
        Assert.True(summary.GetProperty("campaignId").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task GanTaskLechChuDeVaoChienDich_BaoLoi422()
    {
        var managerToken = await GetManagerTokenAsync();
        var eduCampaignId = await TaoChienDichBoiManagerAsync(managerToken, "Education");

        // 1. Tạo trực tiếp task lệch chủ đề vào đợt -> 422
        var createMismatchedRes = await PostAsync("/api/tasks", new
        {
            campaignId = eduCampaignId,
            taskType = "Recording",
            domain = "ItTechnology", // Lệch với Education!
            description = "Task IT trong đợt Giáo dục",
            targetQty = 10,
            deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, managerToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, createMismatchedRes.StatusCode);
        Assert.Equal("task_domain_mismatch_campaign", await LayMaLoiAsync(createMismatchedRes));

        // 2. Tạo task độc lập IT, sau đó attach vào đợt Education -> 422
        var createStandaloneRes = await PostAsync("/api/tasks", new
        {
            campaignId = (long?)null,
            taskType = "Recording",
            domain = "ItTechnology",
            description = "Task độc lập IT",
            targetQty = 10,
            deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, managerToken);
        Assert.Equal(HttpStatusCode.Created, createStandaloneRes.StatusCode);

        using var doc = JsonDocument.Parse(await createStandaloneRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var taskId = doc.RootElement.GetProperty("summary").GetProperty("taskId").GetInt64();

        var attachRes = await PatchAsync($"/api/tasks/{taskId}/campaign", new
        {
            campaignId = eduCampaignId
        }, managerToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, attachRes.StatusCode);
        Assert.Equal("task_domain_mismatch_campaign", await LayMaLoiAsync(attachRes));
    }

    [Fact]
    public async Task XoaTask_DocLapChuaCoViec_ThanhCong()
    {
        var managerToken = await GetManagerTokenAsync();

        var createRes = await PostAsync("/api/tasks", new
        {
            campaignId = (long?)null,
            taskType = "Recording",
            domain = "ItTechnology",
            description = "Task độc lập rảnh để xoá",
            targetQty = 5,
            deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, managerToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var taskId = doc.RootElement.GetProperty("summary").GetProperty("taskId").GetInt64();

        // Xoá -> 204
        var deleteRes = await DeleteAsync($"/api/tasks/{taskId}", managerToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // GET -> 404
        var getRes = await GetAsync($"/api/tasks/{taskId}", managerToken);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task XoaTask_DangTrongChienDich_BaoLoi409()
    {
        var managerToken = await GetManagerTokenAsync();
        var campaignId = await TaoChienDichBoiManagerAsync(managerToken, "ItTechnology");

        var createRes = await PostAsync("/api/tasks", new
        {
            campaignId,
            taskType = "Recording",
            domain = "ItTechnology",
            description = "Task đang nằm trong đợt",
            targetQty = 5,
            deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, managerToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var taskId = doc.RootElement.GetProperty("summary").GetProperty("taskId").GetInt64();

        // Xoá task khi còn trong đợt -> 409
        var deleteRes = await DeleteAsync($"/api/tasks/{taskId}", managerToken);
        Assert.Equal(HttpStatusCode.Conflict, deleteRes.StatusCode);
        Assert.Equal("task_in_campaign", await LayMaLoiAsync(deleteRes));
    }

    [Fact]
    public async Task XoaTask_DaCoNguoiNhan_BaoLoi409()
    {
        var adminToken = await GetAdminTokenAsync();
        var managerToken = await GetManagerTokenAsync();
        var speakerToken = await GetSpeakerTokenAsync();
        var speakerId = await LayUserIdAsync(speakerToken);

        // 1. Admin tạo một câu mẫu (vào thẳng trạng thái Validated)
        var scriptRes = await PostAsync("/api/scripts", new
        {
            csContent = "[vi]Tôi cần [en]meeting [vi]gấp nha",
            veContent = "[vi]Tôi cần cuộc họp gấp nha",
            domain = "ItTechnology",
            alignment = new[]
            {
                new { source = "meeting", target = "cuộc họp", relation = "semantic_equivalent" }
            }
        }, adminToken);
        Assert.Equal(HttpStatusCode.Created, scriptRes.StatusCode);

        using var scriptDoc = JsonDocument.Parse(await scriptRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var scriptId = scriptDoc.RootElement.GetProperty("scriptId").GetString()!;

        // 2. Tạo task độc lập với targetQty = 1
        var createRes = await PostAsync("/api/tasks", new
        {
            campaignId = (long?)null,
            taskType = "Recording",
            domain = "ItTechnology",
            description = "Task có người nhận",
            targetQty = 1,
            deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, managerToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var taskId = doc.RootElement.GetProperty("summary").GetProperty("taskId").GetInt64();

        // 3. Thêm script vào task
        var addRes = await PostAsync($"/api/tasks/{taskId}/items", new { ids = new[] { scriptId } }, managerToken);
        Assert.Equal(HttpStatusCode.OK, addRes.StatusCode);

        // 4. Giao việc cho Speaker
        var assignRes = await PostAsync($"/api/tasks/{taskId}/assign", new { userId = speakerId }, managerToken);
        Assert.Equal(HttpStatusCode.OK, assignRes.StatusCode);

        // 5. Xoá task đã có việc -> 409 task_has_work
        var deleteRes = await DeleteAsync($"/api/tasks/{taskId}", managerToken);
        Assert.Equal(HttpStatusCode.Conflict, deleteRes.StatusCode);
        Assert.Equal("task_has_work", await LayMaLoiAsync(deleteRes));
    }

    // ----------------------------------------------------------------- Trợ giúp

    private async Task<long> TaoChienDichBoiManagerAsync(string managerToken, string domain = "ItTechnology")
    {
        var response = await PostAsync("/api/campaigns", new
        {
            campaignName = $"Đợt test {domain} {Guid.NewGuid():N}",
            domain,
            targetQty = 2000,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        }, managerToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.GetProperty("campaignId").GetInt64();
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
