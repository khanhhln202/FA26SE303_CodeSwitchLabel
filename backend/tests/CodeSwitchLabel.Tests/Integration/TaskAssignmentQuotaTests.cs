using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
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

    private static int? QuotaOf(TaskDetailDto detail, long userId) =>
        Assert.Single(detail.Assignments, a =>
            a.UserId == userId && a.Status == AssignmentStatus.Active).TargetQty;
}
