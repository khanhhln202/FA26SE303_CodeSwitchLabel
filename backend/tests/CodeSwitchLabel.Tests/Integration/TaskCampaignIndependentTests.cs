using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// C1/C2: task độc lập (campaign_id NULL), attach/detach/move, cancel (không xoá cứng).
/// Mỗi test chạy trong transaction riêng (rollback khi Dispose).
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class TaskCampaignIndependentTests : IntegrationTestBase
{
    private readonly DatabaseFixture _fixture;

    public TaskCampaignIndependentTests(DatabaseFixture fixture) : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StandaloneCreate_Succeeds_WithNullCampaign()
    {
        var detail = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = null,
            TaskType = TaskType.Recording,
            Description = "Task độc lập",
            TargetQty = 5,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        Assert.Null(detail.Summary.CampaignId);
        Assert.Null(detail.Summary.CampaignName);
    }

    [Fact]
    public async Task Attach_WithinQuotaAndWindow_Succeeds()
    {
        var standalone = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = null,
            TaskType = TaskType.Recording,
            Description = "Gắn sau",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var attached = await TaskService.AttachAsync(
            standalone.Summary.TaskId, CampaignId, TestContext.Current.CancellationToken,
            ManagerUserId, false);

        Assert.Equal(CampaignId, attached.Summary.CampaignId);
        Assert.NotNull(attached.Summary.CampaignName);
    }

    [Fact]
    public async Task Attach_ExceedsQuota_Fails()
    {
        // Lấp đầy campaign (target 2000) rồi gắn thêm task độc lập target 1.
        var filler = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = CampaignId,
            TaskType = TaskType.Recording,
            Description = "Lấp đầy",
            TargetQty = 2000,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var standalone = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = null,
            TaskType = TaskType.Recording,
            Description = "Vượt quota",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.AttachAsync(standalone.Summary.TaskId, CampaignId,
                TestContext.Current.CancellationToken, ManagerUserId, false));

        Assert.Equal("task_target_exceeds_campaign", ex.Code);
        _ = filler;
    }

    [Fact]
    public async Task Attach_OutsideWindow_Fails()
    {
        var standalone = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = null,
            TaskType = TaskType.Recording,
            Description = "Hạn ngoài khung",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var future = await CreateCampaignAsync(
            $"Khung xa {Guid.NewGuid():N}", 2000,
            DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(60)),
            AdminUserId, CampaignStatus.Open);
        await CampaignService.AssignAsync(future.CampaignId, ManagerUserId, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.AttachAsync(standalone.Summary.TaskId, future.CampaignId,
                TestContext.Current.CancellationToken, ManagerUserId, false));

        Assert.Equal("task_deadline_outside_campaign", ex.Code);
    }

    [Fact]
    public async Task Detach_PreservesTaskAndItems()
    {
        var script = await CreateValidatedScriptAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này.", "[vi]Em nên quét tài liệu này.",
            ScriptDomain.ItTechnology, AdminUserId, 1);

        var created = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = CampaignId,
            TaskType = TaskType.Recording,
            Description = "Gỡ giữ hàng",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        await TaskService.AddItemsAsync(created.Summary.TaskId,
            new AddTaskItemsRequest { Ids = [script.ScriptId] },
            TestContext.Current.CancellationToken, ManagerUserId, false);

        var detached = await CampaignService.DetachTaskAsync(
            CampaignId, created.Summary.TaskId, ManagerUserId, false,
            TestContext.Current.CancellationToken);

        Assert.Null(detached.Summary.CampaignId);

        var row = await Tasks.GetRowAsync(created.Summary.TaskId, TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        Assert.Null(row!.CampaignId);

        var items = await Tasks.GetScriptItemsAsync(created.Summary.TaskId, TestContext.Current.CancellationToken);
        Assert.Contains(items, i => i.ScriptId == script.ScriptId);
    }

    [Fact]
    public async Task DetachViaCampaignEndpoint_DetachesInDatabase()
    {
        // Hồi quy cho bug FE: màn hình đợt phải gọi đúng endpoint detach này,
        // không phải PATCH /api/tasks/{id} với {campaignId: null} (bị backend lờ đi).
        // Toàn bộ đi qua HTTP (mỗi request commit riêng), vì dữ liệu tạo bằng
        // service nằm trong transaction ambient mà connection HTTP không thấy.
        var adminToken = await _fixture.GetAdminTokenAsync();
        var managerToken = await _fixture.GetManagerTokenAsync();
        using var admin = _fixture.CreateClientWithAuth(adminToken);
        using var manager = _fixture.CreateClientWithAuth(managerToken);
        var ct = TestContext.Current.CancellationToken;

        using var me = await manager.GetAsync("/api/auth/me", ct);
        me.EnsureSuccessStatusCode();
        using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync(ct));
        var managerId = meDoc.RootElement.GetProperty("userId").GetInt64();

        var campaignName = $"Detach E2E {Guid.NewGuid():N}";
        using var createCampaign = await admin.PostAsJsonAsync("/api/campaigns",
            new
            {
                campaignName,
                targetQty = 2000,
                startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
            }, ct);
        Assert.Equal(HttpStatusCode.Created, createCampaign.StatusCode);
        using var campaignDoc = JsonDocument.Parse(
            await createCampaign.Content.ReadAsStringAsync(ct));
        var campaignId = campaignDoc.RootElement.GetProperty("campaignId").GetInt64();

        using var assign = await admin.PostAsJsonAsync($"/api/campaigns/{campaignId}/assign",
            new { assignedToUserId = managerId }, ct);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        using var createTask = await manager.PostAsJsonAsync("/api/tasks",
            new
            {
                campaignId,
                taskType = "Recording",
                description = "Gỡ qua endpoint",
                targetQty = 1,
                deadline = DateTimeOffset.UtcNow.AddDays(7)
            }, ct);
        Assert.Equal(HttpStatusCode.Created, createTask.StatusCode);
        using var taskDoc = JsonDocument.Parse(await createTask.Content.ReadAsStringAsync(ct));
        var taskId = taskDoc.RootElement.GetProperty("summary").GetProperty("taskId").GetInt64();

        using var detach = await manager.DeleteAsync(
            $"/api/campaigns/{campaignId}/tasks/{taskId}", ct);
        Assert.Equal(HttpStatusCode.OK, detach.StatusCode);

        using var doc = JsonDocument.Parse(await detach.Content.ReadAsStringAsync(ct));
        Assert.Equal(JsonValueKind.Null,
            doc.RootElement.GetProperty("summary").GetProperty("campaignId").ValueKind);
    }

    [Fact]
    public async Task DeleteCampaign_SetsTasksNull()
    {
        var created = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = CampaignId,
            TaskType = TaskType.Recording,
            Description = "Xoá campaign giữ task",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        // Xoá campaign bằng SQL trực tiếp (không endpoint xoá campaign):
        // FK ON DELETE SET NULL phải giữ lại task.
        var doomedId = created.Summary.CampaignId ?? CampaignId;
        await Db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM campaign WHERE campaign_id = {doomedId}", TestContext.Current.CancellationToken);

        var row = await Tasks.GetRowAsync(created.Summary.TaskId, TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        Assert.Null(row!.CampaignId);
    }

    [Fact]
    public async Task Cancel_PreservesRowAndItems()
    {
        // Không xoá cứng trong project: đóng task độc lập bằng Cancel,
        // hàng và items ở lại làm dấu vết.
        var script = await CreateValidatedScriptAsync(
            "[vi]Anh gửi em cái [en]link [vi]nhé.", "[vi]Anh gửi em cái đường dẫn nhé.",
            ScriptDomain.DailyLife, AdminUserId, 1);

        var created = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = null,
            TaskType = TaskType.Recording,
            Description = "Đóng thay vì xoá",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        await TaskService.AddItemsAsync(created.Summary.TaskId,
            new AddTaskItemsRequest { Ids = [script.ScriptId] },
            TestContext.Current.CancellationToken, ManagerUserId, false);

        var cancelled = await TaskService.CancelAsync(created.Summary.TaskId,
            TestContext.Current.CancellationToken, ManagerUserId, false);

        Assert.Equal(WorkTaskStatus.Cancelled, cancelled.Summary.Status);

        var row = await Tasks.GetRowAsync(created.Summary.TaskId, TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        Assert.Equal(WorkTaskStatus.Cancelled, row!.Status);

        var items = await Tasks.GetScriptItemsAsync(created.Summary.TaskId, TestContext.Current.CancellationToken);
        Assert.Contains(items, i => i.ScriptId == script.ScriptId);
    }

    [Fact]
    public async Task DeleteRoute_IsGone()
    {
        // Route DELETE /api/tasks/{id} đã bị gỡ: API phải trả 404/405, không xoá gì.
        var created = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = null,
            TaskType = TaskType.Recording,
            Description = "Route đã gỡ",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var token = await _fixture.GetManagerTokenAsync();
        using var client = _fixture.CreateClientWithAuth(token);
        using var response = await client.DeleteAsync(
            $"/api/tasks/{created.Summary.TaskId}", TestContext.Current.CancellationToken);

        Assert.True(
            response.StatusCode is System.Net.HttpStatusCode.NotFound
                or System.Net.HttpStatusCode.MethodNotAllowed,
            $"DELETE /api/tasks/{{id}} phải 404/405, nhận {(int)response.StatusCode}.");

        Assert.NotNull(await Tasks.GetRowAsync(created.Summary.TaskId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Progress_ListsUnattachedTasks()
    {
        var created = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = null,
            TaskType = TaskType.Recording,
            Description = "Hiện trong tiến độ",
            TargetQty = 2,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var row = await Tasks.GetRowAsync(created.Summary.TaskId, TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        Assert.Null(row!.CampaignId);
        Assert.Null(row.CampaignName);

        var listed = await TaskService.SearchAsync(
            new TaskSearchRequest { Page = 1, PageSize = 100 },
            TestContext.Current.CancellationToken);
        Assert.Contains(listed.Items, i => i.TaskId == created.Summary.TaskId && i.CampaignId == null);

        // View SQL trực tiếp: unattached task vẫn xuất hiện (LEFT JOIN).
        var inView = await Db.Database
            .SqlQueryRaw<long>("SELECT task_id FROM v_task_progress WHERE task_id = {0}", created.Summary.TaskId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(created.Summary.TaskId, inView);
    }

    [Fact]
    public async Task Move_BetweenCampaigns_RespectsQuota()
    {
        var second = await CreateCampaignAsync(
            $"Đợt hai {Guid.NewGuid():N}", 2000,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            AdminUserId, CampaignStatus.Open);
        await CampaignService.AssignAsync(second.CampaignId, ManagerUserId, TestContext.Current.CancellationToken);

        var created = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = CampaignId,
            TaskType = TaskType.Recording,
            Description = "Chuyển đợt",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var moved = await TaskService.AttachAsync(
            created.Summary.TaskId, second.CampaignId,
            TestContext.Current.CancellationToken, ManagerUserId, false);

        Assert.Equal(second.CampaignId, moved.Summary.CampaignId);
    }
}
