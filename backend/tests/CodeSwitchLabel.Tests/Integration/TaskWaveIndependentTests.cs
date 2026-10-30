using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// C1/C2: task độc lập (wave_id NULL), attach/detach/move, cancel (không xoá cứng).
/// Mỗi test chạy trong transaction riêng (rollback khi Dispose).
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class TaskWaveIndependentTests : IntegrationTestBase
{
    private readonly DatabaseFixture _fixture;

    public TaskWaveIndependentTests(DatabaseFixture fixture) : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StandaloneCreate_Succeeds_WithNullWave()
    {
        var detail = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = null,
            TaskType = TaskType.Recording,
            Description = "Task độc lập",
            TargetQty = 5,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        Assert.Null(detail.Summary.WaveId);
        Assert.Null(detail.Summary.WaveName);
    }

    [Fact]
    public async Task Attach_WithinQuotaAndWindow_Succeeds()
    {
        var standalone = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = null,
            TaskType = TaskType.Recording,
            Description = "Gắn sau",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var attached = await TaskService.AttachAsync(
            standalone.Summary.TaskId, WaveId, TestContext.Current.CancellationToken,
            ManagerUserId, false);

        Assert.Equal(WaveId, attached.Summary.WaveId);
        Assert.NotNull(attached.Summary.WaveName);
    }

    [Fact]
    public async Task Attach_ExceedsQuota_Fails()
    {
        // Lấp đầy wave (target 2000) rồi gắn thêm task độc lập target 1.
        var filler = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = WaveId,
            TaskType = TaskType.Recording,
            Description = "Lấp đầy",
            TargetQty = 2000,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var standalone = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = null,
            TaskType = TaskType.Recording,
            Description = "Vượt quota",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.AttachAsync(standalone.Summary.TaskId, WaveId,
                TestContext.Current.CancellationToken, ManagerUserId, false));

        Assert.Equal("task_target_exceeds_wave", ex.Code);
        _ = filler;
    }

    [Fact]
    public async Task Attach_OutsideWindow_Fails()
    {
        var standalone = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = null,
            TaskType = TaskType.Recording,
            Description = "Hạn ngoài khung",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var future = await CreateWaveAsync(
            $"Khung xa {Guid.NewGuid():N}", 2000,
            DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(60)),
            AdminUserId, WaveStatus.Open);
        await WaveService.AssignAsync(future.WaveId, ManagerUserId, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.AttachAsync(standalone.Summary.TaskId, future.WaveId,
                TestContext.Current.CancellationToken, ManagerUserId, false));

        Assert.Equal("task_deadline_outside_wave", ex.Code);
    }

    [Fact]
    public async Task DeleteFromWave_RemovesTaskAndItems_KeepsRecordingAndReview()
    {
        var script = await CreateValidatedScriptAsync(
            "[vi]Em nên [en]scan [vi]tài liệu này.", "[vi]Em nên quét tài liệu này.",
            ScriptDomain.ItTechnology, AdminUserId, 1);

        var recordingTask = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = WaveId,
            TaskType = TaskType.Recording,
            Domain = ScriptDomain.ItTechnology,
            Description = "Xoá vĩnh viễn task thu âm",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        await TaskService.AddItemsAsync(recordingTask.Summary.TaskId,
            new AddTaskItemsRequest { Ids = [script.ScriptId] },
            TestContext.Current.CancellationToken, ManagerUserId, false);
        await TaskService.AssignAsync(recordingTask.Summary.TaskId, [SpeakerUserId],
            TestContext.Current.CancellationToken, ManagerUserId, false);

        var reviewTask = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = WaveId,
            TaskType = TaskType.Review,
            Domain = ScriptDomain.ItTechnology,
            Description = "Xoá vĩnh viễn task duyệt",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var recording = await CreateRecordingAsync(
            script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);
        recording.TaskId = recordingTask.Summary.TaskId;

        Db.TaskRecordings.Add(new TaskRecording
        {
            TaskId = reviewTask.Summary.TaskId,
            RecordingId = recording.RecordingId,
            Status = TaskRecordingStatus.Queued,
            IncludedAt = DateTimeOffset.UtcNow
        });
        var review = new Review
        {
            RecordingId = recording.RecordingId,
            ReviewerId = ReviewerUserId,
            TaskId = reviewTask.Summary.TaskId,
            ReviewRound = 1,
            Decision = ReviewDecision.Approved,
            IsBlind = true,
            ReviewedAt = DateTimeOffset.UtcNow
        };
        Db.Reviews.Add(review);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await WaveService.DeleteTaskAsync(WaveId, recordingTask.Summary.TaskId,
            ManagerUserId, false, TestContext.Current.CancellationToken);
        await WaveService.DeleteTaskAsync(WaveId, reviewTask.Summary.TaskId,
            ManagerUserId, false, TestContext.Current.CancellationToken);

        Assert.Null(await Tasks.GetRowAsync(recordingTask.Summary.TaskId, TestContext.Current.CancellationToken));
        Assert.Null(await Tasks.GetRowAsync(reviewTask.Summary.TaskId, TestContext.Current.CancellationToken));

        var keptRecording = await Db.Recordings
            .FirstAsync(r => r.RecordingId == recording.RecordingId, TestContext.Current.CancellationToken);
        Assert.Null(keptRecording.TaskId);

        var keptReview = await Db.Reviews
            .FirstAsync(rv => rv.ReviewId == review.ReviewId, TestContext.Current.CancellationToken);
        Assert.Null(keptReview.TaskId);

        // Lượt giao, mục công việc và hàng đợi xoá theo task.
        Assert.False(await Db.TaskAssignments.AnyAsync(
            a => a.TaskId == recordingTask.Summary.TaskId, TestContext.Current.CancellationToken));
        Assert.False(await Db.TaskScripts.AnyAsync(
            ts => ts.TaskId == recordingTask.Summary.TaskId, TestContext.Current.CancellationToken));
        Assert.False(await Db.TaskRecordings.AnyAsync(
            tr => tr.TaskId == reviewTask.Summary.TaskId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteViaWaveEndpoint_RemovesTask()
    {
        // Hồi quy cho bug FE: màn hình đợt phải gọi đúng endpoint này,
        // không phải PATCH /api/tasks/{id} với {waveId: null} (bị backend lờ đi).
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

        var waveName = $"Xoá task E2E {Guid.NewGuid():N}";
        using var createWave = await admin.PostAsJsonAsync("/api/waves",
            new
            {
                waveName,
                domain = "ItTechnology", // domain is required
                targetQty = 2000,
                startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
            }, ct);
        Assert.Equal(HttpStatusCode.Created, createWave.StatusCode);
        using var waveDoc = JsonDocument.Parse(
            await createWave.Content.ReadAsStringAsync(ct));
        var waveId = waveDoc.RootElement.GetProperty("waveId").GetInt64();

        using var assign = await admin.PostAsJsonAsync($"/api/waves/{waveId}/assign",
            new { assignedToUserId = managerId }, ct);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        using var createTask = await manager.PostAsJsonAsync("/api/tasks",
            new
            {
                waveId,
                taskType = "Recording",
                description = "Xoá qua endpoint",
                targetQty = 1,
                deadline = DateTimeOffset.UtcNow.AddDays(7)
            }, ct);
        Assert.Equal(HttpStatusCode.Created, createTask.StatusCode);
        using var taskDoc = JsonDocument.Parse(await createTask.Content.ReadAsStringAsync(ct));
        var taskId = taskDoc.RootElement.GetProperty("summary").GetProperty("taskId").GetInt64();

        using var delete = await manager.DeleteAsync(
            $"/api/waves/{waveId}/tasks/{taskId}", ct);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var get = await manager.GetAsync($"/api/tasks/{taskId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task DeleteFromWave_NotWaveOwner_ForbiddenAndTaskRemains()
    {
        var created = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = WaveId,
            TaskType = TaskType.Recording,
            Domain = ScriptDomain.ItTechnology,
            Description = "Task của manager khác",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            WaveService.DeleteTaskAsync(WaveId, created.Summary.TaskId,
                AdminUserId, false, TestContext.Current.CancellationToken));

        Assert.Equal("task_not_owned", ex.Code);
        Assert.NotNull(await Tasks.GetRowAsync(created.Summary.TaskId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteWave_SetsTasksNull()
    {
        var created = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = WaveId,
            TaskType = TaskType.Recording,
            Description = "Xoá wave giữ task",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        // Xoá wave bằng SQL trực tiếp (không endpoint xoá wave):
        // FK ON DELETE SET NULL phải giữ lại task.
        var doomedId = created.Summary.WaveId ?? WaveId;
        await Db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM wave WHERE wave_id = {doomedId}", TestContext.Current.CancellationToken);

        var row = await Tasks.GetRowAsync(created.Summary.TaskId, TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        Assert.Null(row!.WaveId);
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
            WaveId = null,
            TaskType = TaskType.Recording,
            Domain = ScriptDomain.DailyLife, // match script domain
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
            WaveId = null,
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
            WaveId = null,
            TaskType = TaskType.Recording,
            Description = "Hiện trong tiến độ",
            TargetQty = 2,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var row = await Tasks.GetRowAsync(created.Summary.TaskId, TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        Assert.Null(row!.WaveId);
        Assert.Null(row.WaveName);

        var listed = await TaskService.SearchAsync(
            new TaskSearchRequest { Page = 1, PageSize = 100 },
            ct: TestContext.Current.CancellationToken);
        Assert.Contains(listed.Items, i => i.TaskId == created.Summary.TaskId && i.WaveId == null);

        // View SQL trực tiếp: unattached task vẫn xuất hiện (LEFT JOIN).
        var inView = await Db.Database
            .SqlQueryRaw<long>("SELECT task_id FROM v_task_progress WHERE task_id = {0}", created.Summary.TaskId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(created.Summary.TaskId, inView);
    }

    [Fact]
    public async Task Move_BetweenWaves_RespectsQuota()
    {
        var second = await CreateWaveAsync(
            $"Đợt hai {Guid.NewGuid():N}", 2000,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            AdminUserId, WaveStatus.Open);
        await WaveService.AssignAsync(second.WaveId, ManagerUserId, TestContext.Current.CancellationToken);

        var created = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = WaveId,
            TaskType = TaskType.Recording,
            Description = "Chuyển đợt",
            TargetQty = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var moved = await TaskService.AttachAsync(
            created.Summary.TaskId, second.WaveId,
            TestContext.Current.CancellationToken, ManagerUserId, false);

        Assert.Equal(second.WaveId, moved.Summary.WaveId);
    }
}
