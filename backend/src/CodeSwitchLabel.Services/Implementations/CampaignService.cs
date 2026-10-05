using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Implementations;

/// <summary>
/// Quản lý chiến dịch thu thập — đơn vị kế hoạch mà mọi task phải thuộc về.
/// Các ràng buộc chỉ tiêu/ngày với task con phần lớn do trigger của database chặn; ở đây
/// kiểm trước những trường hợp dễ gặp để trả thông báo rõ ràng thay vì lỗi thô từ database.
/// </summary>
public class CampaignService(
    ICampaignRepository campaigns,
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
            request.Status, assignedTo, request.Page, request.PageSize, ct);

        var ids = items.Select(c => c.CampaignId).ToList();
        var progress = (await campaigns.GetProgressAsync(ids, ct))
            .ToDictionary(p => p.CampaignId);

        var mapped = items.Select(c =>
        {
            progress.TryGetValue(c.CampaignId, out var p);

            return new CampaignListItemDto(
                c.CampaignId, c.CampaignName, c.TargetQty, c.StartDate, c.EndDate, c.Status, c.CreatedAt,
                p?.AllocatedTaskQty ?? 0, (int)(p?.TaskCount ?? 0), (int)(p?.CompletedTaskCount ?? 0), c.AssignedTo);
        }).ToList();

        return new PagedResult<CampaignListItemDto>(mapped, request.Page, request.PageSize, total);
    }

    public async Task<CampaignDto> GetAsync(long campaignId, CancellationToken ct = default)
    {
        var campaign = await campaigns.GetAsync(campaignId, ct) ?? throw NotFound(campaignId);
        return ToDto(campaign);
    }

    public async Task<CampaignDto> CreateAsync(
        CreateCampaignRequest request, long createdById, CancellationToken ct = default)
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
            TargetQty = request.TargetQty!.Value,
            StartDate = start,
            EndDate = end,
            Status = CampaignStatus.Draft,
            CreatedBy = createdById,
            CreatedAt = clock.GetUtcNow()
        };

        campaigns.Add(campaign);
        await campaigns.SaveChangesAsync(ct);

        return ToDto(campaign);
    }

    public async Task<CampaignDto> UpdateAsync(
        long campaignId, UpdateCampaignRequest request, CancellationToken ct = default)
    {
        var campaign = await campaigns.GetForUpdateAsync(campaignId, ct) ?? throw NotFound(campaignId);

        if (request.CampaignName is not null) campaign.CampaignName = request.CampaignName.Trim();
        if (request.Status.HasValue) campaign.Status = request.Status.Value;
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

    private static CampaignDto ToDto(Campaign c) =>
        new(c.CampaignId, c.CampaignName, c.TargetQty, c.StartDate, c.EndDate, c.Status, c.CreatedBy, c.AssignedTo, c.CreatedAt);

    private static NotFoundException NotFound(long campaignId) =>
        new("campaign_not_found", $"Không tìm thấy chiến dịch #{campaignId}.");
}
