using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Implementations;

/// <summary>
/// Quản lý chiến dịch thu thập — task có thể độc lập (campaign_id NULL) rồi gắn sau.
/// Các ràng buộc chỉ tiêu/ngày với task con (khi đang gắn) phần lớn do trigger của database chặn;
/// ở đây kiểm trước những trường hợp dễ gặp để trả thông báo rõ ràng thay vì lỗi thô từ database.
/// </summary>
public class CampaignService(
    ICampaignRepository campaigns,
    ITaskService tasks,
    TimeProvider clock) : ICampaignService
{
    public async Task<PagedResult<CampaignListItemDto>> SearchAsync(
        CampaignSearchRequest request, long? userId, RoleName? userRole, CancellationToken ct = default)
    {
        long? assignedTo = null;
        if (userRole == RoleName.TaskManager && userId.HasValue)
        {
            assignedTo = userId.Value;
        }

        var (items, total) = await campaigns.SearchAsync(
            request.Status, assignedTo, request.Page, request.PageSize,
            request.ToSort<CampaignSortField>(), ct);

        var ids = items.Select(c => c.CampaignId).ToList();
        var progress = (await campaigns.GetProgressAsync(ids, ct))
            .ToDictionary(p => p.CampaignId);

        var mapped = items.Select(c =>
        {
            progress.TryGetValue(c.CampaignId, out var p);

            // Truyền c.Domain sang CampaignListItemDto
            return new CampaignListItemDto(
                c.CampaignId, c.CampaignName, c.Domain, c.TargetQty, c.StartDate, c.EndDate, c.Status, c.CreatedAt,
                p?.AllocatedTaskQty ?? 0, (int)(p?.TaskCount ?? 0), (int)(p?.CompletedTaskCount ?? 0), c.AssignedTo);
        }).ToList();

        return new PagedResult<CampaignListItemDto>(mapped, request.Page, request.PageSize, total);
    }

    public async Task<CampaignDto> GetAsync(long campaignId, CancellationToken ct = default)
    {
        var campaign = await campaigns.GetAsync(campaignId, ct) ?? throw NotFound(campaignId);
        return ToDto(campaign);
    }

    // Hỗ trợ Task Manager tạo đợt (auto-assign cho chính mình nếu không phải Admin)
    public async Task<CampaignDto> CreateAsync(
        CreateCampaignRequest request, long createdById, bool isAdmin, CancellationToken ct = default)
    {
        var start = request.StartDate!.Value;
        var end = request.EndDate!.Value;

        if (start > end)
        {
            throw new UnprocessableException(
                "campaign_dates_invalid", "Ngày bắt đầu chiến dịch phải trước hoặc bằng ngày kết thúc.");
        }

        var campaign = new Campaign
        {
            CampaignName = request.CampaignName.Trim(),
            Domain = request.Domain ?? throw new UnprocessableException("domain_required", "Phải chọn chủ đề (domain) cho chiến dịch."),
            TargetQty = request.TargetQty!.Value,
            StartDate = start,
            EndDate = end,
            // đợt mới tạo chưa có nhiệm vụ nào nên là Open. Sau đó trạng thái
            // đợt là hàm của các task, tự tính lại ở CampaignRepository.RefreshStatusAsync.
            Status = CampaignStatus.Open,
            CreatedBy = createdById,
            // Task Manager tự tạo đợt thì tự động gán cho chính mình để có quyền tạo/gắn task
            AssignedTo = isAdmin ? null : createdById,
            CreatedAt = clock.GetUtcNow()
        };

        campaigns.Add(campaign);
        await campaigns.SaveChangesAsync(ct);

        return ToDto(campaign);
    }

    // Kiểm tra quyền sở hữu khi sửa đợt (TaskManager chỉ sửa đợt của mình)
    public async Task<CampaignDto> UpdateAsync(
        long campaignId, UpdateCampaignRequest request, long? callerId, bool isAdmin, CancellationToken ct = default)
    {
        var campaign = await campaigns.GetForUpdateAsync(campaignId, ct) ?? throw NotFound(campaignId);
        EnsureCampaignOwner(campaign, callerId, isAdmin);

        if (request.CampaignName is not null) campaign.CampaignName = request.CampaignName.Trim();
        if (request.Domain.HasValue) campaign.Domain = request.Domain.Value;

        // UpdateCampaignRequest không còn trường `status`. Trạng thái đợt là hàm
        // của các task bên trong nên không bấm tay được — sửa tên/chủ đề/ngày không được đổi nó,
        // và nó cũng không cần đổi vì những thay đổi đó không đụng tới task.

        if (request.StartDate.HasValue) campaign.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) campaign.EndDate = request.EndDate.Value;

        if (campaign.StartDate > campaign.EndDate)
        {
            throw new UnprocessableException(
                "campaign_dates_invalid", "Ngày bắt đầu chiến dịch phải trước hoặc bằng ngày kết thúc.");
        }

        if (request.TargetQty.HasValue)
        {
            // Không hạ chỉ tiêu xuống dưới phần đã chia cho các task — trigger của database cũng chặn,
            // nhưng kiểm ở đây để trả lỗi rõ ràng.
            var allocated = await campaigns.SumAllocatedAsync(campaignId, ct);

            if (request.TargetQty.Value < allocated)
            {
                throw new UnprocessableException(
                    "campaign_target_below_allocated",
                    $"Chỉ tiêu {request.TargetQty.Value} nhỏ hơn {allocated} đã chia cho các task của chiến dịch.");
            }

            campaign.TargetQty = request.TargetQty.Value;
        }

        await campaigns.SaveChangesAsync(ct);
        return ToDto(campaign);
    }

    // Xoá chiến dịch — task con thành độc lập nhờ ON DELETE SET NULL
    public async Task DeleteAsync(
        long campaignId, long? callerId, bool isAdmin, CancellationToken ct = default)
    {
        var campaign = await campaigns.GetForUpdateAsync(campaignId, ct) ?? throw NotFound(campaignId);
        EnsureCampaignOwner(campaign, callerId, isAdmin);

        campaigns.Remove(campaign);
        await campaigns.SaveChangesAsync(ct);
    }

    public async Task<CampaignDto> AssignAsync(
        long campaignId, long? assignedToUserId, CancellationToken ct = default)
    {
        var campaign = await campaigns.GetForUpdateAsync(campaignId, ct) ?? throw NotFound(campaignId);

        if (assignedToUserId.HasValue)
        {
            var isTaskManager = await campaigns.IsTaskManagerAsync(assignedToUserId.Value, ct);
            if (!isTaskManager)
            {
                throw new UnprocessableException(
                    "assigned_to_invalid_role",
                    "Chỉ giao được chiến dịch cho Task Manager. Người bạn chọn đang giữ vai khác.");
            }
        }

        campaign.AssignedTo = assignedToUserId;

        await campaigns.SaveChangesAsync(ct);
        return ToDto(campaign);
    }

    /// <summary>Xoá vĩnh viễn task khỏi chiến dịch. Giữ lại bản ghi thu âm và lượt duyệt.</summary>
    public Task DeleteTaskAsync(
        long campaignId, long taskId, long? callerId, bool isAdmin, CancellationToken ct = default)
        => tasks.DeleteFromCampaignAsync(campaignId, taskId, ct, callerId, isAdmin);

    /// <summary>Task thu âm duy nhất của chiến dịch, tạo nếu chưa có.</summary>
    public Task<TaskDetailDto> GetOrCreateRecordingTaskAsync(
        long campaignId, bool autoFill, long callerId, bool isAdmin, CancellationToken ct = default)
        => tasks.GetOrCreateRecordingTaskAsync(campaignId, autoFill, callerId, isAdmin, ct);

    private static void EnsureCampaignOwner(Campaign campaign, long? callerId, bool isAdmin)
    {
        if (!callerId.HasValue || isAdmin) return;
        if (campaign.AssignedTo == callerId.Value || campaign.CreatedBy == callerId.Value) return;

        throw new ForbiddenException(
            "campaign_not_owned",
            $"Chiến dịch #{campaign.CampaignId} không thuộc quyền quản lý của bạn.");
    }

    private static CampaignDto ToDto(Campaign c) =>
        new(c.CampaignId, c.CampaignName, c.Domain, c.TargetQty, c.StartDate, c.EndDate, c.Status, c.CreatedBy, c.AssignedTo, c.CreatedAt);

    private static NotFoundException NotFound(long campaignId) =>
        new("campaign_not_found", $"Không tìm thấy chiến dịch #{campaignId}.");
}
