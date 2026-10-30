using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.WorkTasks;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// TEAM_010 — hàng đợi văn bản chung và đa người nhận task.
///
/// Những test ở đây ghim hành vi mà yêu cầu đặt ra, không chỉ hành vi code đang có:
///   • mọi câu văn bản phải nằm trong hàng đợi, theo đúng thứ tự thời gian (FIFO)
///   • hai người KHÔNG BAO GIỜ nhận cùng một câu
///   • xoá cặp thì câu rút khỏi mọi hàng đợi
///   • task giao được cho nhiều người
///   • bản ghi bị từ chối trả câu về hàng đợi và mở cho Speaker khác
/// </summary>
[Trait("Category", "Integration")]
[Collection("Database")]
public sealed class ScriptQueueAndMultiAssigneeTests(DatabaseFixture fixture)
    : IntegrationTestBase(fixture)
{
    private IScriptQueueRepository Queue =>
        Scope.ServiceProvider.GetRequiredService<IScriptQueueRepository>();

    private async Task<List<AppUser>> TaoBaSpeakerAsync() =>
    [
        await CreateUserAsync($"queue-sp1-{Guid.NewGuid():N}@test.local", RoleName.Speaker),
        await CreateUserAsync($"queue-sp2-{Guid.NewGuid():N}@test.local", RoleName.Speaker),
        await CreateUserAsync($"queue-sp3-{Guid.NewGuid():N}@test.local", RoleName.Speaker)
    ];

    /// <summary>
    /// Container database dùng chung cho mọi test tích hợp, nên hàng đợi luôn có câu
    /// của test khác. Test phải lọc ra câu CỦA MÌNH thì mới khẳng định được — giả định
    /// hàng đợi rỗng sẽ làm test pass/fail tuỳ thứ tự chạy.
    /// </summary>
    private async Task<List<string>> ClaimNhieuLanAsync(long userId, int max = 60)
    {
        var taken = new List<string>();

        for (var i = 0; i < max; i++)
        {
            var claim = await Queue.ClaimNextAsync(
                userId, QueueStage.TextReview, null, TestContext.Current.CancellationToken);

            if (claim is null) break;
            taken.Add(claim.ScriptId);
        }

        return taken;
    }

    // ------------------------------------------------------------------ hàng đợi

    [Fact]
    public async Task CauMoi_VaHangDoiDuyetVanBan_KhongTuDongQua()
    {
        // Arrange
        var speaker = await CreateUserAsync($"q1-{Guid.NewGuid():N}@test.local", RoleName.Speaker);

        // Act
        var script = await CreateScriptAsync(
            "[vi]Anh [en]scan [vi]toi", "[vi]Anh quet toi", ScriptDomain.ItTechnology, speaker.UserId);

        // Assert — trigger fn_script_queue_sync tự tạo hàng, không cần code gọi.
        var row = await Queue.GetOpenRowAsync(
            script.ScriptId, QueueStage.TextReview, TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        Assert.Equal(QueueState.Queued, row!.State);
    }

    [Fact]
    public async Task LayCauTheoThuTuFIFO_TheoThuTuVaoHangDoi()
    {
        // Arrange
        var speaker = await CreateUserAsync($"q2-{Guid.NewGuid():N}@test.local", RoleName.Speaker);

        var scripts = new List<Script>();
        foreach (var word in new[] { "banana", "camera", "delta" })
        {
            scripts.Add(await CreateScriptAsync(
                $"[vi]Anh [en]{word} [vi]toi", $"[vi]Anh {word} toi",
                ScriptDomain.DailyLife, speaker.UserId));
        }

        // Act — claim liên tiếp rồi lọc ra đúng ba câu của test này.
        var taken = await ClaimNhieuLanAsync(speaker.UserId);
        var mine = scripts.Select(s => s.ScriptId)
            .Where(taken.Contains)
            .OrderBy(taken.IndexOf)
            .ToArray();

        // Assert — thứ tự lấy về trùng thứ tự tạo. Mã câu sinh từ sequence nên vẫn
        // tăng đều, nhưng thứ tự FIFO là do enqueued_at quyết định chứ không phải mã.
        Assert.Equal([.. scripts.Select(s => s.ScriptId)], mine);
    }

    [Fact]
    public async Task HaiNguoiKhongTheNhanCungMotCau()
    {
        // Arrange
        var speakers = await TaoBaSpeakerAsync();
        var (s1, s2) = (speakers[0], speakers[1]);

        var a = await CreateScriptAsync("[vi]Anh [en]alpha [vi]toi", "[vi]Anh alpha toi", ScriptDomain.ItTechnology, s1.UserId);
        var b = await CreateScriptAsync("[vi]Anh [en]bravo [vi]toi", "[vi]Anh bravo toi", ScriptDomain.ItTechnology, s1.UserId);

        // Act — hai người claim gần như cùng lúc.
        var first = await ClaimNhieuLanAsync(s1.UserId);
        var second = await ClaimNhieuLanAsync(s2.UserId);

        // Assert — không câu nào nằm trong cả hai danh sách (yêu cầu 3.2).
        //
        // KHÔNG đòi mỗi người phải có câu: hàng đợi là FIFO nên người gọi trước có thể
        // lấy hết, khi đó người sau nhận rỗng là đúng. Cần bảo đảm là không câu nào bị
        // giao cho hai người, và hai câu của test không bị rơi mất.
        Assert.Empty(first.Intersect(second));
        Assert.Contains(a.ScriptId, first.Union(second));
        Assert.Contains(b.ScriptId, first.Union(second));
    }

    [Fact]
    public async Task CauDaClaim_ChuaHetHan_NguoiKhacKhongLayDuoc()
    {
        // Arrange
        var speakers = await TaoBaSpeakerAsync();
        var script = await CreateScriptAsync(
            "[vi]Anh [en]solo [vi]toi", "[vi]Anh solo toi", ScriptDomain.ItTechnology, speakers[0].UserId);

        // Người 0 đi lấy hết, trong đó có câu của test.
        var mine = await ClaimNhieuLanAsync(speakers[0].UserId);
        Assert.Contains(script.ScriptId, mine);

        // Act — người khác tới trong lúc claim của người 0 còn hiệu lực.
        var theirs = await ClaimNhieuLanAsync(speakers[1].UserId);

        // Assert
        Assert.DoesNotContain(script.ScriptId, theirs);
    }

    [Fact]
    public async Task ClaimHetHan_CauQuaLaiDuocLay()
    {
        // Arrange
        var speakers = await TaoBaSpeakerAsync();
        var script = await CreateScriptAsync(
            "[vi]Anh [en]expire [vi]toi", "[vi]Anh expire toi", ScriptDomain.ItTechnology, speakers[0].UserId);

        await ClaimNhieuLanAsync(speakers[0].UserId);

        // Giả lập người đã mở câu rồi bỏ ngang quá lâu.
        await Db.Database.ExecuteSqlRawAsync(
            "UPDATE script_queue SET claim_expires_at = now() - interval '1 minute' WHERE script_id = {0}",
            script.ScriptId);

        // Act
        var retaken = await ClaimNhieuLanAsync(speakers[1].UserId);

        // Assert
        Assert.Contains(script.ScriptId, retaken);
    }

    [Fact]
    public async Task CauDaDuyetXong_RotKhoiDuyetVanBanVaSangChangGhiAm()
    {
        // Arrange
        var speaker = await CreateUserAsync($"q5-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var script = await CreateScriptAsync(
            "[vi]Anh [en]next [vi]toi", "[vi]Anh next toi", ScriptDomain.ItTechnology, speaker.UserId);

        // Act — duyệt nội dung đúng luồng trigger.
        Db.ScriptReviews.Add(new ScriptReview
        {
            ScriptId = script.ScriptId,
            UserId = ReviewerUserId,
            Action = ScriptReviewAction.Accepted,
            ReviewedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        script.Status = ScriptStatus.Validated;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var textRow = await Db.ScriptQueue.FirstAsync(
            q => q.ScriptId == script.ScriptId && q.Stage == QueueStage.TextReview,
            TestContext.Current.CancellationToken);
        var recRow = await Db.ScriptQueue.FirstAsync(
            q => q.ScriptId == script.ScriptId && q.Stage == QueueStage.Recording,
            TestContext.Current.CancellationToken);

        Assert.Equal(QueueState.Dropped, textRow.State);
        Assert.Equal(QueueState.Queued, recRow.State);
    }

    [Fact]
    public async Task XoaCaCap_CauRutKhoiMoiHangDoi()
    {
        // Arrange
        var speaker = await CreateUserAsync($"q6-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var script = await CreateScriptAsync(
            "[vi]Anh [en]drop [vi]toi", "[vi]Anh drop toi", ScriptDomain.ItTechnology, speaker.UserId);

        // Act — soft-delete: xoá cả văn bản lẫn audio (nút "Xóa hoàn toàn").
        script.Status = ScriptStatus.Deactivated;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var open = await Db.ScriptQueue.CountAsync(
            q => q.ScriptId == script.ScriptId &&
                 (q.State == QueueState.Queued || q.State == QueueState.Claimed),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, open);
    }

    // ------------------------------------------------------- đa người nhận task

    /// <summary>
    /// Tạo một đợt đã giao cho Task Manager + task ghi âm CHƯA có ai nhận, kèm sẵn mục
    /// công việc. Luật có sẵn: chỉ tiêu phải không vượt số mục làm được mới giao được,
    /// nên task rỗng thì không test được vòng giao.
    /// </summary>
    private async Task<WorkTask> TaoTaskGhiAmAsync(string nhan, int soMuc = 3)
    {
        var manager = await CreateUserAsync($"tm-{nhan}-{Guid.NewGuid():N}@test.local", RoleName.TaskManager);
        var speaker = await CreateUserAsync($"sp-{nhan}-{Guid.NewGuid():N}@test.local", RoleName.Speaker);

        var wave = await CreateWaveAsync($"Đợt {nhan}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), manager.UserId, WaveStatus.Open);

        wave.AssignedTo = manager.UserId;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var task = await CreateTaskAsync(wave.WaveId, TaskType.Recording, soMuc,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));

        foreach (var word in new[] { "alpha", "bravo", "charlie" }.Take(soMuc))
        {
            var script = await CreateValidatedScriptAsync(
                $"[vi]Anh [en]{word} [vi]toi", $"[vi]Anh {word} toi",
                ScriptDomain.ItTechnology, speaker.UserId);

            Db.TaskScripts.Add(new TaskScript
            {
                TaskId = task.TaskId,
                ScriptId = script.ScriptId,
                Status = TaskScriptStatus.Pending
            });
        }

        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return task;
    }

    /// <summary>
    /// task duyệt TRONG đợt với hàng chờ TRỐNG — đúng thế "giao việc trước
    /// khi có bản ghi" mà đợt nào cũng gặp lúc khởi tạo.
    /// </summary>
    private async Task<WorkTask> TaoTaskDuyetTrongDotAsync(string nhan, int chiTieu = 3)
    {
        var manager = await CreateUserAsync($"tm-{nhan}-{Guid.NewGuid():N}@test.local", RoleName.TaskManager);

        var wave = await CreateWaveAsync($"Đợt {nhan}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), manager.UserId, WaveStatus.Open);

        wave.AssignedTo = manager.UserId;
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return await CreateTaskAsync(wave.WaveId, TaskType.Review, chiTieu,
            manager.UserId, DateTimeOffset.UtcNow.AddDays(10));
    }

    [Fact]
    public async Task TaskGiaoChoNhieuNguoi_CaNguoiDungNhan()
    {
        // Arrange
        var speakers = await TaoBaSpeakerAsync();
        var task = await TaoTaskGhiAmAsync("nhieu");

        // Act
        var detail = await TaskService.AssignAsync(
            task.TaskId, [speakers[0].UserId, speakers[1].UserId, speakers[2].UserId],
            TestContext.Current.CancellationToken, task.CreatedBy);

        // Assert
        Assert.Equal(3, detail.Summary.Assignees.Count);
        Assert.Equal(3, detail.Assignments.Count(a => a.Status == AssignmentStatus.Active));

        foreach (var speaker in speakers)
        {
            Assert.True(await Tasks.IsActiveTaskOfAsync(
                task.TaskId, speaker.UserId, TaskType.Recording, TestContext.Current.CancellationToken));
        }
    }

    /// <summary>
    /// TEAM_018 — yêu cầu user: "RV vẫn trong hàng chờ dù chưa có bản ghi". Giao task duyệt
    /// trong đợt lúc hàng chờ trống phải THÀNH CÔNG; bản ghi sẽ tự vào khi Speaker nộp.
    /// Trước đây luật "chỉ tiêu không vượt số mục" chặn bằng 422 target_exceeds_items.
    /// </summary>
    [Fact]
    public async Task GiaoTaskDuyetTrongDot_KhiHangChoTrong_VanChoPhep()
    {
        // Arrange
        var task = await TaoTaskDuyetTrongDotAsync("duyet-trong");
        var reviewer = await CreateUserAsync($"rv-empty-{Guid.NewGuid():N}@test.local", RoleName.Reviewer);

        // Act
        var detail = await TaskService.AssignAsync(
            task.TaskId, [reviewer.UserId], TestContext.Current.CancellationToken, task.CreatedBy);

        // Assert — có lượt giao Active thật, không phải chỉ không ném lỗi.
        Assert.Contains(detail.Assignments, a => a.Status == AssignmentStatus.Active);
        Assert.True(await Tasks.IsActiveTaskOfAsync(
            task.TaskId, reviewer.UserId, TaskType.Review, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Task duyệt NGOÀI đợt không có đường tự đẩy nào — hàng chờ trống vẫn bị chặn như cũ,
    /// nới luật TEAM_018 chỉ áp cho task duyệt thuộc đợt.
    /// </summary>
    [Fact]
    public async Task GiaoTaskDuyetNgoaiDot_KhiHangChoTrong_VanChan()
    {
        // Arrange
        var standalone = await TaskService.CreateAsync(new CreateTaskRequest
        {
            WaveId = null,
            TaskType = TaskType.Review,
            Description = "Duyệt độc lập, hàng chờ trống",
            TargetQty = 3,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, TestContext.Current.CancellationToken);

        var reviewer = await CreateUserAsync($"rv-alone-{Guid.NewGuid():N}@test.local", RoleName.Reviewer);

        // Act
        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.AssignAsync(standalone.Summary.TaskId, [reviewer.UserId],
                TestContext.Current.CancellationToken, ManagerUserId));

        // Assert
        Assert.Equal("target_exceeds_items", ex.Code);
    }

    [Fact]
    public async Task GiaoLaiChoNguoiDangNhan_ChiBaoLoiKhongDuplicate()
    {
        // Arrange
        var speakers = await TaoBaSpeakerAsync();
        var task = await TaoTaskGhiAmAsync("trung");

        await TaskService.AssignAsync(task.TaskId, [speakers[0].UserId],
            TestContext.Current.CancellationToken, task.CreatedBy);

        // Act — giao lại đúng người đang có (no-op, không ném lỗi).
        var result = await TaskService.AssignAsync(
            task.TaskId, [speakers[0].UserId], TestContext.Current.CancellationToken, task.CreatedBy);

        // Assert — trả về chi tiết task bình thường, không ném.
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
    }

    [Fact]
    public async Task GoNguoiCuoiCungRaKhoiTaskDaCoViec_ThiNoiDung()
    {
        // Arrange
        var speaker = await CreateUserAsync($"solo-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var task = await TaoTaskGhiAmAsync("golast");

        await TaskService.AssignAsync(task.TaskId, [speaker.UserId],
            TestContext.Current.CancellationToken, task.CreatedBy);

        // Task đã có mục công việc — gỡ người cuối sẽ đẩy task về nháp, không ai làm tiếp.
        var script = await CreateValidatedScriptAsync(
            "[vi]Anh [en]busy [vi]toi", "[vi]Anh busy toi", ScriptDomain.ItTechnology, speaker.UserId);

        Db.TaskScripts.Add(new TaskScript
        {
            TaskId = task.TaskId,
            ScriptId = script.ScriptId,
            Status = TaskScriptStatus.Pending
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var error = await Assert.ThrowsAnyAsync<AppException>(() => TaskService.UnassignAsync(
            task.TaskId, speaker.UserId, TestContext.Current.CancellationToken, task.CreatedBy));

        // Assert
        Assert.Equal("cannot_remove_last_assignee", error.Code);
    }

    [Fact]
    public async Task GoMotNguoiKhoiTaskNhieuNguoi_HoanTat()
    {
        // Arrange
        var speakers = await TaoBaSpeakerAsync();
        var task = await TaoTaskGhiAmAsync("gomot");

        await TaskService.AssignAsync(task.TaskId, [speakers[0].UserId, speakers[1].UserId],
            TestContext.Current.CancellationToken, task.CreatedBy);

        // Act
        var detail = await TaskService.UnassignAsync(
            task.TaskId, speakers[0].UserId, TestContext.Current.CancellationToken, task.CreatedBy);

        // Assert — người gỡ còn trong lịch sử, người kia vẫn nhận.
        Assert.Single(detail.Summary.Assignees);
        Assert.Equal(speakers[1].UserId, detail.Summary.Assignees[0].UserId);
        Assert.Contains(detail.Assignments, a =>
            a.UserId == speakers[0].UserId && a.Status == AssignmentStatus.Reassigned);
    }

    // ------------------------------ hàng đợi ghi âm theo chiến dịch

    [Fact]
    public async Task GetNext_TrongTask_PhamViCaChienDich()
    {
        var ct = TestContext.Current.CancellationToken;
        var speakers = await TaoBaSpeakerAsync();
        var (a, b, c) = (speakers[0], speakers[1], speakers[2]);

        var wave = await CreateWaveAsync(
            $"Đợt queue-{Guid.NewGuid():N}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), ManagerUserId, WaveStatus.Open);
        wave.AssignedTo = ManagerUserId;
        await Db.SaveChangesAsync(ct);

        var t1 = await CreateTaskAsync(wave.WaveId, TaskType.Recording, 2, ManagerUserId);
        var t2 = await CreateTaskAsync(wave.WaveId, TaskType.Recording, 2, ManagerUserId);

        // Câu của task 2 vào hàng trước, câu của task 1 sau.
        var s2 = await CreateValidatedScriptAsync(
            "[vi]Thu [en]queue2 [vi]nhe", "[vi]Thu queue2 nhe", ScriptDomain.ItTechnology, a.UserId);
        var s1 = await CreateValidatedScriptAsync(
            "[vi]Thu [en]queue1 [vi]nhe", "[vi]Thu queue1 nhe", ScriptDomain.ItTechnology, a.UserId);

        Db.TaskScripts.AddRange(
            new TaskScript { TaskId = t2.TaskId, ScriptId = s2.ScriptId, Status = TaskScriptStatus.Pending },
            new TaskScript { TaskId = t1.TaskId, ScriptId = s1.ScriptId, Status = TaskScriptStatus.Pending });
        Db.TaskAssignments.AddRange(
            new TaskAssignment
            {
                TaskId = t1.TaskId, UserId = a.UserId,
                AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow
            },
            new TaskAssignment
            {
                TaskId = t2.TaskId, UserId = b.UserId,
                AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow
            });
        await Db.SaveChangesAsync(ct);

        // Đợt phát câu chung: A nhận task 1 vẫn thấy câu cũ hơn nằm trong task 2.
        var first = await ScriptAssignmentService.GetNextAsync(a.UserId, t1.TaskId, ct);
        Assert.Equal(s2.ScriptId, first?.ScriptId);

        // Không còn giữ chỗ nên B cũng nhận đúng câu đó — mỗi người một bản riêng.
        var second = await ScriptAssignmentService.GetNextAsync(b.UserId, t2.TaskId, ct);
        Assert.Equal(s2.ScriptId, second?.ScriptId);

        // Người ngoài đợt không mượn task để lấy câu.
        var error = await Assert.ThrowsAsync<ForbiddenException>(
            () => ScriptAssignmentService.GetNextAsync(c.UserId, t1.TaskId, ct));
        Assert.Equal("task_not_recordable", error.Code);
    }

    /// <summary>
    /// Câu đã đủ hai bản chờ duyệt/đạt không được chiếm hết hạn mức lấy câu kế tiếp.
    /// Trước khi lọc trong SQL, hơn 10 mục đầu đã thu đủ làm lần gọi trả rỗng oan.
    /// </summary>
    [Fact]
    public async Task GetNext_BoQuaCauDaDuHaiBienThe()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = (await TaoBaSpeakerAsync())[0];

        var wave = await CreateWaveAsync(
            $"Đợt full-{Guid.NewGuid():N}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), ManagerUserId, WaveStatus.Open);
        wave.AssignedTo = ManagerUserId;
        await Db.SaveChangesAsync(ct);

        var task = await CreateTaskAsync(wave.WaveId, TaskType.Recording, 11, ManagerUserId);
        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = task.TaskId, UserId = speaker.UserId,
            AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow
        });

        var scripts = new List<Script>();
        for (var i = 0; i < 11; i++)
        {
            var script = await CreateValidatedScriptAsync(
                $"[vi]Đủ [en]full{i} [vi]câu", $"[vi]Đủ full{i} câu",
                ScriptDomain.ItTechnology, speaker.UserId);
            scripts.Add(script);

            Db.TaskScripts.Add(new TaskScript
            {
                TaskId = task.TaskId, ScriptId = script.ScriptId,
                AssigneeUserId = speaker.UserId, Status = TaskScriptStatus.Pending
            });

            if (i < 10)
            {
                await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
                await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);
            }
        }

        await Db.SaveChangesAsync(ct);

        var next = await ScriptAssignmentService.GetNextAsync(speaker.UserId, task.TaskId, ct);

        Assert.Equal(scripts[10].ScriptId, next?.ScriptId);
    }

    // ------------------------------ bản ghi bị từ chối thì ai được thu lại

    /// <summary>
    /// Reviewer loại audio là loại cả lần thu: bản `rejected` không còn giữ cặp câu,
    /// nên Speaker khác được thu lại. Người cũ vẫn còn nợ biến thể còn lại.
    /// </summary>
    [Fact]
    public async Task BanGhiBiTuChoi_CauMoLaiChoNguoiKhac()
    {
        // Arrange — speaker A thu một bản rồi bị từ chối.
        var ct = TestContext.Current.CancellationToken;
        var speakers = await TaoBaSpeakerAsync();
        var (a, b) = (speakers[0], speakers[1]);

        var script = await CreateValidatedScriptAsync(
            "[vi]Thu [en]retry [vi]lai nhe", "[vi]Thu thu lai nhe",
            ScriptDomain.ItTechnology, a.UserId);

        var recording = await CreateRecordingAsync(script.ScriptId, a.UserId, SentenceVariant.CodeSwitching);
        recording.Status = RecordingStatus.Rejected;
        await Db.SaveChangesAsync(ct);

        // Bản rejected không giữ cặp câu.
        Assert.False(await Scripts.IsRecordedByOtherAsync(script.ScriptId, b.UserId, ct));
        Assert.False(await Recordings.HasActiveRecordingAsync(
            script.ScriptId, SentenceVariant.CodeSwitching, a.UserId, ct));

        // Người cũ vẫn còn việc (thiếu biến thể vi) và bản ghi vẫn còn trong lịch sử.
        Assert.False(await Scripts.IsRecordedByOtherAsync(script.ScriptId, a.UserId, ct));
        Assert.Single(await Scripts.GetRecordingsOfSpeakerAsync(script.ScriptId, a.UserId, ct));
    }

    /// <summary>
    /// Mỗi Speaker tự nhận lại câu sau khi bản của mình bị loại — không phải chờ
    /// người khác thử trước, vì mỗi người có bản ghi riêng trên cùng cặp câu.
    /// </summary>
    [Fact]
    public async Task BanGhiBiTuChoi_MoiNguoiNhanLaiDocLap()
    {
        var ct = TestContext.Current.CancellationToken;
        var speakers = await TaoBaSpeakerAsync();
        var (a, b) = (speakers[0], speakers[1]);

        var wave = await CreateWaveAsync(
            $"Đợt retry-{Guid.NewGuid():N}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), ManagerUserId, WaveStatus.Open);
        wave.AssignedTo = ManagerUserId;
        await Db.SaveChangesAsync(ct);
        var task = await CreateTaskAsync(wave.WaveId, TaskType.Recording, 1, ManagerUserId);

        var script = await CreateValidatedScriptAsync(
            "[vi]Thu [en]deadend [vi]nhe", "[vi]Thu deadend nhe",
            ScriptDomain.ItTechnology, a.UserId);

        Db.TaskScripts.Add(new TaskScript
        {
            TaskId = task.TaskId, ScriptId = script.ScriptId, Status = TaskScriptStatus.Pending
        });
        Db.TaskAssignments.AddRange(
            new TaskAssignment
            {
                TaskId = task.TaskId, UserId = a.UserId,
                AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow
            },
            new TaskAssignment
            {
                TaskId = task.TaskId, UserId = b.UserId,
                AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow
            });
        await Db.SaveChangesAsync(ct);

        var rejected = await CreateRecordingAsync(script.ScriptId, a.UserId, SentenceVariant.CodeSwitching);
        rejected.Status = RecordingStatus.Rejected;
        await Db.SaveChangesAsync(ct);

        // A nhận lại được ngay, không phải chờ B.
        var aClaim = await Queue.ClaimNextAsync(a.UserId, QueueStage.Recording, task.TaskId, ct);
        Assert.Equal(script.ScriptId, aClaim?.ScriptId);
        await Queue.ReleaseAsync(script.ScriptId, QueueStage.Recording, ct: ct);

        // B cũng nhận được cùng câu — mỗi người một bản riêng.
        var bClaim = await Queue.ClaimNextAsync(b.UserId, QueueStage.Recording, task.TaskId, ct);
        Assert.Equal(script.ScriptId, bClaim?.ScriptId);

        // B cũng bị loại: trả câu về hàng, A vẫn nhận lại được.
        var bRejected = await CreateRecordingAsync(script.ScriptId, b.UserId, SentenceVariant.PureVietnamese);
        bRejected.Status = RecordingStatus.Rejected;
        await Db.SaveChangesAsync(ct);
        await Queue.ReleaseAsync(script.ScriptId, QueueStage.Recording, ct: ct);

        var again = await Queue.ClaimNextAsync(a.UserId, QueueStage.Recording, task.TaskId, ct);
        Assert.Equal(script.ScriptId, again?.ScriptId);
    }

    /// <summary>
    /// Người đang giữ cặp bằng một bản chờ duyệt vẫn nhận được biến thể còn lại dù từng
    /// bị loại trên câu này. Bản của người này không chặn Speaker khác thu cùng câu.
    /// </summary>
    [Fact]
    public async Task NguoiDangGiuMotBienThe_KhongChanNguoiKhac()
    {
        var ct = TestContext.Current.CancellationToken;
        var speakers = await TaoBaSpeakerAsync();
        var (a, b) = (speakers[0], speakers[1]);

        var wave = await CreateWaveAsync(
            $"Đợt rerecord-{Guid.NewGuid():N}", 2000,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), ManagerUserId, WaveStatus.Open);
        wave.AssignedTo = ManagerUserId;
        await Db.SaveChangesAsync(ct);
        var task = await CreateTaskAsync(wave.WaveId, TaskType.Recording, 1, ManagerUserId);

        var script = await CreateValidatedScriptAsync(
            "[vi]Thu [en]variant [vi]nhe", "[vi]Thu variant nhe",
            ScriptDomain.ItTechnology, a.UserId);

        Db.TaskScripts.Add(new TaskScript
        {
            TaskId = task.TaskId, ScriptId = script.ScriptId, Status = TaskScriptStatus.Pending
        });
        Db.TaskAssignments.AddRange(
            new TaskAssignment
            {
                TaskId = task.TaskId, UserId = a.UserId,
                AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow
            },
            new TaskAssignment
            {
                TaskId = task.TaskId, UserId = b.UserId,
                AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow
            });
        await Db.SaveChangesAsync(ct);

        // A thu bản đầu, bị loại, rồi ghi lại thẳng một biến thể (không qua hàng đợi).
        var rejected = await CreateRecordingAsync(script.ScriptId, a.UserId, SentenceVariant.CodeSwitching);
        rejected.Status = RecordingStatus.Rejected;
        await Db.SaveChangesAsync(ct);
        await Queue.ReleaseAsync(script.ScriptId, QueueStage.Recording, ct: ct);

        await CreateRecordingAsync(script.ScriptId, a.UserId, SentenceVariant.PureVietnamese);

        // A nhận biến thể còn lại; bản đang chờ của A không chặn B.
        var aClaim = await Queue.ClaimNextAsync(a.UserId, QueueStage.Recording, task.TaskId, ct);
        Assert.Equal(script.ScriptId, aClaim?.ScriptId);

        await Queue.ReleaseAsync(script.ScriptId, QueueStage.Recording, ct: ct);
        var bClaim = await Queue.ClaimNextAsync(b.UserId, QueueStage.Recording, task.TaskId, ct);
        Assert.Equal(script.ScriptId, bClaim?.ScriptId);
    }

    // ------------------------------ gỡ người khỏi task

    /// <summary>
    /// Gỡ người khỏi task thu âm: nhả câu họ đang giữ để người khác lấy ngay, nhưng
    /// KHÔNG đụng bản đã nộp — bản đó vẫn thuộc về họ và vẫn chặn người khác.
    /// </summary>
    [Fact]
    public async Task GoNguoiKhoiTask_NhaCauDangGiuVaGiuBanDaNop()
    {
        var ct = TestContext.Current.CancellationToken;
        var speakers = await TaoBaSpeakerAsync();
        var (a, b) = (speakers[0], speakers[1]);

        var task = await TaoTaskGhiAmAsync("release", soMuc: 2);
        await TaskService.AssignAsync(task.TaskId, [a.UserId, b.UserId], ct, task.CreatedBy);

        // A giữ một câu trong hàng đợi.
        var claim = await Queue.ClaimNextAsync(a.UserId, QueueStage.Recording, task.TaskId, ct);
        Assert.NotNull(claim);

        // Và đã nộp một câu khác.
        var items = await Db.TaskScripts
            .Where(ts => ts.TaskId == task.TaskId)
            .ToListAsync(ct);
        var submitted = items.Single(ts => ts.ScriptId != claim!.ScriptId);
        var recording = await CreateRecordingAsync(
            submitted.ScriptId, a.UserId, SentenceVariant.CodeSwitching);

        // Act
        await TaskService.UnassignAsync(task.TaskId, a.UserId, ct, task.CreatedBy);

        // Assert — câu đang giữ được nhả về hàng đợi...
        var row = await Db.ScriptQueue.FirstAsync(
            q => q.ScriptId == claim!.ScriptId && q.Stage == QueueStage.Recording, ct);
        Assert.Equal(QueueState.Queued, row.State);
        Assert.Null(row.ClaimedBy);

        // ...còn bản đã nộp vẫn nguyên của A.
        var kept = await Db.Recordings.FirstAsync(r => r.RecordingId == recording.RecordingId, ct);
        Assert.Equal(a.UserId, kept.SpeakerId);
        Assert.Equal(RecordingStatus.PendingReview, kept.Status);
    }

    /// <summary>
    /// Bản ghi bị từ chối thì câu quay lại HÀNG ĐỢI ghi âm và được đẩy xuống CUỐI hàng
    /// đợi — không đứng lại đầu, nếu không bị claim lại mãi.
    /// </summary>
    [Fact]
    public async Task BanGhiBiTuChoi_CauQuayLaiHangDoiVaDuocDayXuongCuoi()
    {
        // Arrange — câu đã thu đủ nên hàng của nó ở trạng thái 'done'.
        var speaker = await CreateUserAsync($"sp-requeue-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var script = await CreateValidatedScriptAsync(
            "[vi]Thu [en]again [vi]nhe", "[vi]Thu lai nhe",
            ScriptDomain.ItTechnology, speaker.UserId);

        await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);
        await CreateRecordingAsync(script.ScriptId, speaker.UserId, SentenceVariant.PureVietnamese);

        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Db.Database.ExecuteSqlRawAsync(
            "UPDATE script_queue SET state = 'done', enqueued_at = now() - interval '1 hour' WHERE script_id = {0} AND stage = 'recording'",
            script.ScriptId);

        // Act — mô phỏng nút "Chỉ xóa audio".
        await Queue.RequeueAsync(script.ScriptId, QueueStage.Recording, TestContext.Current.CancellationToken);

        // Assert — hàng mở lại, và mốc FIFO được đặt lại nên câu không giành chỗ đầu.
        var row = await Db.ScriptQueue.FirstAsync(
            q => q.ScriptId == script.ScriptId && q.Stage == QueueStage.Recording,
            TestContext.Current.CancellationToken);

        Assert.Equal(QueueState.Queued, row.State);
        Assert.True(row.EnqueuedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    // --------------------------------------------------------- trừ KPI khi từ chối
    [Fact]
    public async Task BanGhiBiTuChoi_MucCauLuiLaiVaTaskRoiTrangThaiHoanThanh()
    {
        // Arrange — task chỉ tiêu 1, đã giao cho speaker, câu đã thu đủ và đã tính xong.
        var speaker = await CreateUserAsync($"sp-kpi-{Guid.NewGuid():N}@test.local", RoleName.Speaker);
        var task = await TaoTaskGhiAmAsync("kpi", soMuc: 1);

        await TaskService.AssignAsync(task.TaskId, [speaker.UserId],
            TestContext.Current.CancellationToken, task.CreatedBy);

        var item = await Db.TaskScripts.FirstAsync(
            ts => ts.TaskId == task.TaskId, TestContext.Current.CancellationToken);

        var recording = await CreateRecordingAsync(item.ScriptId, speaker.UserId, SentenceVariant.CodeSwitching);

        // Gắn bản ghi vào task để `Started` > 0 — nếu không thì task đánh giá là
        // "đã giao, chưa làm gì" và rơi về Open thay vì InProgress.
        recording.TaskId = task.TaskId;

        item.Status = TaskScriptStatus.Completed;

        var recItem = new TaskRecording
        {
            TaskId = task.TaskId,
            RecordingId = recording.RecordingId,
            Status = TaskRecordingStatus.Reviewed
        };
        Db.TaskRecordings.Add(recItem);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Chỉ tiêu đã đạt -> task ở Completed. Đây đúng tình huống yêu cầu 4.2 mô tả:
        // 100 xuống 95 thì phải tự rời "Hoàn thành".
        await TaskService.UpdateAsync(task.TaskId,
            new CodeSwitchLabel.Services.Dtos.UpdateTaskRequest { TargetQty = 1 },
            TestContext.Current.CancellationToken, task.CreatedBy);

        await Db.Entry(task).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkTaskStatus.Completed, task.Status);

        // Act
        await Scope.ServiceProvider.GetRequiredService<ITaskProgressTracker>()
            .OnReviewSubmittedAsync(
                recording, RecordingStatus.Rejected, task.TaskId, TestContext.Current.CancellationToken);

        // Assert — mục câu lùi về Pending (người đọc phải thu bù), mục duyệt thành
        // Skipped vì bản ghi đã chốt nên không ai duyệt lại được, và task rời Completed.
        Assert.Equal(TaskScriptStatus.Pending, item.Status);
        Assert.Equal(TaskRecordingStatus.Skipped, recItem.Status);

        await Db.Entry(task).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkTaskStatus.InProgress, task.Status);
    }
}
