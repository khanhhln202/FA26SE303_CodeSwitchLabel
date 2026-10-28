using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Quota theo từng người nhận task và luồng tạo task thu âm duy nhất cho chiến dịch.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class TaskAssignmentQuotaTests : IntegrationTestBase
{
    private IScriptQueueRepository Queue =>
        Scope.ServiceProvider.GetRequiredService<IScriptQueueRepository>();

    public TaskAssignmentQuotaTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task CampaignRecordingTask_GoiLai_KhongTaoTaskThuHai()
    {
        var ct = TestContext.Current.CancellationToken;

        var first = await CampaignService.GetOrCreateRecordingTaskAsync(
            CampaignId, autoFill: false, ManagerUserId, isAdmin: false, ct);
        var second = await CampaignService.GetOrCreateRecordingTaskAsync(
            CampaignId, autoFill: false, ManagerUserId, isAdmin: false, ct);

        Assert.Equal(first.Summary.TaskId, second.Summary.TaskId);
        Assert.Equal(TaskType.Recording, second.Summary.TaskType);
        Assert.Equal(CampaignId, second.Summary.CampaignId);
        Assert.Equal(2000, second.Summary.Progress.TargetQty);
        Assert.Equal("Test Campaign", second.Summary.Description);

        var count = await Db.WorkTasks.CountAsync(
            t => t.CampaignId == CampaignId && t.TaskType == TaskType.Recording, ct);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AutoFillContinuesAfterCandidateIsQueuedInAnotherUnassignedTask()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateValidatedScriptAsync("[vi]Em nhớ [en]upload alpha nhé.", "[vi]Em nhớ tải lên alpha nhé.",
            ScriptDomain.ItTechnology, AdminUserId);
        await CreateValidatedScriptAsync("[vi]Em nhớ [en]upload beta nhé.", "[vi]Em nhớ tải lên beta nhé.",
            ScriptDomain.ItTechnology, AdminUserId);

        var source = await CreateTaskAsync(CampaignId, TaskType.Recording, 1, ManagerUserId);
        var target = await CreateTaskAsync(CampaignId, TaskType.Recording, 1, ManagerUserId);
        var candidates = await Tasks.FindScriptCandidatesAsync(
            source.TaskId, [], ScriptDomain.ItTechnology, 2, [], ct);
        Assert.Equal(2, candidates.Count);

        await TaskService.AddItemsAsync(source.TaskId,
            new AddTaskItemsRequest { Ids = [candidates[0]] }, ct, ManagerUserId);

        var result = await TaskService.AddItemsAsync(target.TaskId,
            new AddTaskItemsRequest { AutoFill = new AutoFillCriteria { Count = 1 } },
            ct, ManagerUserId);

        Assert.Equal(1, result.Added);
        Assert.Contains(result.Skipped, item => item.Id == candidates[0]);
        Assert.Contains(result.Task.Items, item => item.Id == candidates[1]);
    }

    /// <summary>
    /// Bản rejected trả cặp về hàng đợi, nên người nhận task khác được thêm câu đó —
    /// trước đây điều kiện chỉ bỏ qua bản trượt QC nên câu bị chặn nhầm.
    /// </summary>
    [Fact]
    public async Task AddItems_CauCoBanBiTuChoiCuaNguoiKhac_VanNhan()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await CreateUserWithBuilderAsync(RoleName.Speaker, NewUniqueEmail("add-owner"), "Add Owner");
        var assignee = await CreateUserWithBuilderAsync(RoleName.Speaker, NewUniqueEmail("add-assignee"), "Add Assignee");

        var script = await CreateValidatedScriptAsync(
            "[vi]Câu [en]add [vi]này", "[vi]Câu add này",
            ScriptDomain.ItTechnology, owner.UserId);

        var rejected = await CreateRecordingAsync(script.ScriptId, owner.UserId, SentenceVariant.CodeSwitching);
        rejected.Status = RecordingStatus.Rejected;
        var task = await CreateTaskAsync(CampaignId, TaskType.Recording, 1, ManagerUserId);
        Db.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = task.TaskId, UserId = assignee.UserId,
            AssignmentStatus = AssignmentStatus.Active, AssignedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(ct);

        var result = await TaskService.AddItemsAsync(task.TaskId,
            new AddTaskItemsRequest { Ids = [script.ScriptId] }, ct, ManagerUserId);

        Assert.Equal(1, result.Added);
        Assert.Contains(result.Task.Items, item => item.Id == script.ScriptId);
    }

    [Fact]
    public async Task SetAssignments_ThayDanhSach_ChiaQuotaVaGiuLichSu()
    {
        var ct = TestContext.Current.CancellationToken;
        var task = await CreateTaskAsync(CampaignId, TaskType.Review, 10, ManagerUserId);
        var reviewers = new[]
        {
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Reviewer A"),
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Reviewer B"),
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Reviewer C")
        };

        var detail = await TaskService.SetAssignmentsAsync(
            task.TaskId,
            new SetTaskAssignmentsRequest
            {
                Assignments =
                [
                    new AssignmentQuotaRequest { UserId = reviewers[0].UserId },
                    new AssignmentQuotaRequest { UserId = reviewers[1].UserId },
                    new AssignmentQuotaRequest { UserId = reviewers[2].UserId }
                ]
            },
            ct, ManagerUserId, isAdmin: false);

        Assert.Equal(4, QuotaOf(detail, reviewers[0].UserId));
        Assert.Equal(3, QuotaOf(detail, reviewers[1].UserId));
        Assert.Equal(3, QuotaOf(detail, reviewers[2].UserId));

        // Giữ reviewer B với quota 6, để trống reviewer A -> phần còn lại 4.
        detail = await TaskService.SetAssignmentsAsync(
            task.TaskId,
            new SetTaskAssignmentsRequest
            {
                Assignments =
                [
                    new AssignmentQuotaRequest { UserId = reviewers[1].UserId, TargetQty = 6 },
                    new AssignmentQuotaRequest { UserId = reviewers[0].UserId }
                ]
            },
            ct, ManagerUserId, isAdmin: false);

        Assert.Equal(4, QuotaOf(detail, reviewers[0].UserId));
        Assert.Equal(6, QuotaOf(detail, reviewers[1].UserId));

        var active = detail.Assignments.Where(a => a.Status == AssignmentStatus.Active).ToList();
        Assert.Equal(2, active.Count);
        Assert.DoesNotContain(active, a => a.UserId == reviewers[2].UserId);

        var reassigned = Assert.Single(
            detail.Assignments,
            a => a.UserId == reviewers[2].UserId && a.Status == AssignmentStatus.Reassigned);
        Assert.Equal(3, reassigned.TargetQty);
    }

    [Fact]
    public async Task Assign_ThemNguoi_ChiaLaiQuotaCaNhom()
    {
        var ct = TestContext.Current.CancellationToken;
        var task = await CreateTaskAsync(CampaignId, TaskType.Review, 10, ManagerUserId);
        var reviewers = new[]
        {
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Reviewer D"),
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Reviewer E"),
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Reviewer F")
        };

        var detail = await TaskService.AssignAsync(
            task.TaskId, [reviewers[0].UserId, reviewers[1].UserId],
            ct, ManagerUserId, isAdmin: false);

        Assert.Equal(5, QuotaOf(detail, reviewers[0].UserId));
        Assert.Equal(5, QuotaOf(detail, reviewers[1].UserId));

        detail = await TaskService.AssignAsync(
            task.TaskId, [reviewers[2].UserId], ct, ManagerUserId, isAdmin: false);

        var quotas = new[]
        {
            QuotaOf(detail, reviewers[0].UserId),
            QuotaOf(detail, reviewers[1].UserId),
            QuotaOf(detail, reviewers[2].UserId)
        }.Order().ToArray();

        Assert.Equal([3, 3, 4], quotas);
        Assert.Equal(10, quotas.Sum());
    }

    [Fact]
    public async Task SetAssignments_DatDuQuotaNhungTongKhacChiTieu_BaoLoi()
    {
        var ct = TestContext.Current.CancellationToken;
        var task = await CreateTaskAsync(CampaignId, TaskType.Review, 10, ManagerUserId);
        var reviewers = new[]
        {
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Reviewer G"),
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Reviewer H")
        };

        // Thiếu 1: tổng 9 nhưng chỉ tiêu 10.
        var thieu = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.SetAssignmentsAsync(
                task.TaskId,
                new SetTaskAssignmentsRequest
                {
                    Assignments =
                    [
                        new AssignmentQuotaRequest { UserId = reviewers[0].UserId, TargetQty = 4 },
                        new AssignmentQuotaRequest { UserId = reviewers[1].UserId, TargetQty = 5 }
                    ]
                },
                ct, ManagerUserId, isAdmin: false));

        Assert.Equal("assignment_quota_not_equal_target", thieu.Code);

        // Thừa 1: tổng 11, vượt chỉ tiêu.
        var thua = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.SetAssignmentsAsync(
                task.TaskId,
                new SetTaskAssignmentsRequest
                {
                    Assignments =
                    [
                        new AssignmentQuotaRequest { UserId = reviewers[0].UserId, TargetQty = 4 },
                        new AssignmentQuotaRequest { UserId = reviewers[1].UserId, TargetQty = 7 }
                    ]
                },
                ct, ManagerUserId, isAdmin: false));

        Assert.Equal("assignment_quota_exceeds_target", thua.Code);
    }

    [Fact]
    public async Task Assign_TaskThuAm_MoiSpeakerNhanQuotaRieng()
    {
        var ct = TestContext.Current.CancellationToken;
        var scriptIds = new List<string>();

        for (var i = 0; i < 4; i++)
        {
            var script = await CreateValidatedScriptAsync(
                $"[vi]Em nên [en]scan{i} [vi]tài liệu này.",
                $"[vi]Em nên quét tài liệu này {i}.",
                ScriptDomain.ItTechnology, AdminUserId);

            scriptIds.Add(script.ScriptId);
        }

        var created = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = CampaignId,
            TaskType = TaskType.Recording,
            Description = "Quota riêng cho Speaker",
            TargetQty = 4,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, ct);

        await TaskService.AddItemsAsync(
            created.Summary.TaskId,
            new AddTaskItemsRequest { Ids = scriptIds },
            ct, ManagerUserId, isAdmin: false);

        var speakers = new[]
        {
            await CreateUserWithBuilderAsync(RoleName.Speaker, fullName: "Speaker Q1"),
            await CreateUserWithBuilderAsync(RoleName.Speaker, fullName: "Speaker Q2")
        };

        var detail = await TaskService.AssignAsync(
            created.Summary.TaskId, [speakers[0].UserId, speakers[1].UserId],
            ct, ManagerUserId, isAdmin: false);

        Assert.Equal(2, QuotaOf(detail, speakers[0].UserId));
        Assert.Equal(2, QuotaOf(detail, speakers[1].UserId));
    }

    [Fact]
    public async Task CreateRecording_WithAssignees_10Target3Speakers_4_3_3_AndQueueIsolation()
    {
        var ct = TestContext.Current.CancellationToken;
        var scriptIds = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var s = await CreateValidatedScriptAsync(
                $"[vi]Em [en]test{i} [vi]câu này.", $"[vi]Em câu này {i}.", ScriptDomain.ItTechnology, AdminUserId);
            scriptIds.Add(s.ScriptId);
        }
        var speakers = new[]
        {
            await CreateUserWithBuilderAsync(RoleName.Speaker, fullName: "Sp A"),
            await CreateUserWithBuilderAsync(RoleName.Speaker, fullName: "Sp B"),
            await CreateUserWithBuilderAsync(RoleName.Speaker, fullName: "Sp C")
        };

        var detail = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = CampaignId,
            TaskType = TaskType.Recording,
            Description = "Ghi âm 10 cặp 3 người",
            TargetQty = 10,
            Deadline = DateTimeOffset.UtcNow.AddDays(7),
            AssigneeUserIds = [speakers[0].UserId, speakers[1].UserId, speakers[2].UserId]
        }, ManagerUserId, ct);

        Assert.Equal(10, detail.Summary.Progress.TargetQty);
        Assert.Equal(3, detail.Assignments.Count(a => a.Status == AssignmentStatus.Active));
        Assert.Equal(4, QuotaOf(detail, speakers[0].UserId));
        Assert.Equal(3, QuotaOf(detail, speakers[1].UserId));
        Assert.Equal(3, QuotaOf(detail, speakers[2].UserId));

        var items = await Db.TaskScripts
            .Where(ts => ts.TaskId == detail.Summary.TaskId)
            .ToListAsync(ct);
        Assert.Equal(10, items.Count);
        Assert.Equal(4, items.Count(i => i.AssigneeUserId == speakers[0].UserId));
        Assert.Equal(3, items.Count(i => i.AssigneeUserId == speakers[1].UserId));
        Assert.Equal(3, items.Count(i => i.AssigneeUserId == speakers[2].UserId));
    }

    [Fact]
    public async Task CreateReview_WithAssignees_10Target3Reviewers_4_3_3_EmptyQueue()
    {
        var ct = TestContext.Current.CancellationToken;
        var reviewers = new[]
        {
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Rv A"),
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Rv B"),
            await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Rv C")
        };
        var detail = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = CampaignId,
            TaskType = TaskType.Review,
            Description = "Duyệt 10 bản ghi 3 người",
            TargetQty = 10,
            Deadline = DateTimeOffset.UtcNow.AddDays(7),
            AssigneeUserIds = [reviewers[0].UserId, reviewers[1].UserId, reviewers[2].UserId]
        }, ManagerUserId, ct);

        Assert.Equal(3, detail.Assignments.Count(a => a.Status == AssignmentStatus.Active));
        Assert.Equal(4, QuotaOf(detail, reviewers[0].UserId));
        Assert.Equal(3, QuotaOf(detail, reviewers[1].UserId));
        Assert.Equal(3, QuotaOf(detail, reviewers[2].UserId));
    }

    [Fact]
    public async Task CreateRecording_InsufficientPairs_ThrowsNotEnoughPairs()
    {
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 2; i++)
        {
            await CreateValidatedScriptAsync(
                $"[vi]Em [en]p{i} [vi]t.", $"[vi]Em t {i}.", ScriptDomain.ItTechnology, AdminUserId);
        }
        var speaker = await CreateUserWithBuilderAsync(RoleName.Speaker, fullName: "Sp Only");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.CreateAsync(new CreateTaskRequest
            {
                CampaignId = CampaignId,
                TaskType = TaskType.Recording,
                TargetQty = 10,
                Deadline = DateTimeOffset.UtcNow.AddDays(7),
                AssigneeUserIds = [speaker.UserId]
            }, ManagerUserId, ct));
        Assert.Equal("recording_not_enough_pairs", ex.Code);
    }

    [Fact]
    public async Task CreateTask_TargetBelowAssigneeCount_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var speaker = await CreateUserWithBuilderAsync(RoleName.Speaker, fullName: "Sp Only2");
        var speaker2 = await CreateUserWithBuilderAsync(RoleName.Speaker, fullName: "Sp Only3");

        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.CreateAsync(new CreateTaskRequest
            {
                CampaignId = CampaignId,
                TaskType = TaskType.Recording,
                TargetQty = 1,
                Deadline = DateTimeOffset.UtcNow.AddDays(7),
                AssigneeUserIds = [speaker.UserId, speaker2.UserId]
            }, ManagerUserId, ct));
        Assert.Equal("target_less_than_assignees", ex.Code);
    }

    [Fact]
    public async Task CreateTask_InvalidMember_RoleMismatch_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var reviewer = await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Rv Bad");
        var ex = await Assert.ThrowsAsync<UnprocessableException>(() =>
            TaskService.CreateAsync(new CreateTaskRequest
            {
                CampaignId = CampaignId,
                TaskType = TaskType.Recording,
                TargetQty = 5,
                Deadline = DateTimeOffset.UtcNow.AddDays(7),
                AssigneeUserIds = [reviewer.UserId]
            }, ManagerUserId, ct));
        Assert.Equal("assignee_role_mismatch", ex.Code);
    }

    [Fact]
    public async Task EditRecording_AssigneeChanged_ReleasesClaimedPairs_KeepsSubmitted()
    {
        var ct = TestContext.Current.CancellationToken;
        var scriptIds = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var s = await CreateValidatedScriptAsync(
                $"[vi]Em [en]e{i} [vi]t.", $"[vi]Em t {i}.", ScriptDomain.ItTechnology, AdminUserId);
            scriptIds.Add(s.ScriptId);
        }
        var speakers = new[]
        {
            await CreateUserWithBuilderAsync(RoleName.Speaker, fullName: "Sp A"),
            await CreateUserWithBuilderAsync(RoleName.Speaker, fullName: "Sp B")
        };

        var task = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = CampaignId,
            TaskType = TaskType.Recording,
            TargetQty = 4,
            Deadline = DateTimeOffset.UtcNow.AddDays(7),
            AssigneeUserIds = [speakers[0].UserId, speakers[1].UserId]
        }, ManagerUserId, ct);

        await TaskService.AddItemsAsync(
            task.Summary.TaskId, new AddTaskItemsRequest { Ids = scriptIds }, ct, ManagerUserId, isAdmin: false);

        // A đang giữ một câu trong hàng đợi và đã nộp một câu khác.
        var claim = await Queue.ClaimNextAsync(
            speakers[0].UserId, QueueStage.Recording, task.Summary.TaskId, ct);
        Assert.NotNull(claim);

        var recording = await CreateRecordingAsync(
            scriptIds[0], speakers[0].UserId, SentenceVariant.CodeSwitching);

        // Act — sửa bảng phân công chỉ còn B.
        var detail = await TaskService.SetAssignmentsAsync(task.Summary.TaskId, new SetTaskAssignmentsRequest
        {
            Assignments = [new AssignmentQuotaRequest { UserId = speakers[1].UserId }]
        }, ct, ManagerUserId, isAdmin: false);

        Assert.Equal(speakers[1].UserId, Assert.Single(detail.Summary.Assignees).UserId);

        // Câu đang giữ được nhả ngay cho người khác lấy.
        var row = await Db.ScriptQueue.FirstAsync(
            q => q.ScriptId == claim!.ScriptId && q.Stage == QueueStage.Recording, ct);
        Assert.Equal(QueueState.Queued, row.State);
        Assert.Null(row.ClaimedBy);

        // Bản đã nộp vẫn còn nguyên của A.
        Assert.NotNull(await Db.Recordings.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RecordingId == recording.RecordingId, ct));
    }

    [Fact]
    public async Task Legacy_NoSelection_KeepsCurrentFlow()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await TaskService.CreateAsync(new CreateTaskRequest
        {
            CampaignId = CampaignId,
            TaskType = TaskType.Review,
            Description = "Legacy no assignee",
            TargetQty = 5,
            Deadline = DateTimeOffset.UtcNow.AddDays(7)
        }, ManagerUserId, ct);
        Assert.Empty(result.Assignments);
        Assert.Equal(5, result.Summary.Progress.TargetQty);
    }

    private static int? QuotaOf(TaskDetailDto detail, long userId) =>
        Assert.Single(detail.Assignments, a =>
            a.UserId == userId && a.Status == AssignmentStatus.Active).TargetQty;
}
