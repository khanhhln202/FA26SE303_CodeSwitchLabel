using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

public interface ICampaignRepository
{
    void Add(Campaign campaign);

    /// <summary>
    /// Xoá chiến dịch. Task gắn vào KHÔNG bị xoá: FK task.campaign_id là
    /// ON DELETE SET NULL nên chúng tự thành task độc lập.
    /// </summary>
    void Remove(Campaign campaign);

    Task<Campaign?> GetForUpdateAsync(long campaignId, CancellationToken ct = default);
    Task<Campaign?> GetAsync(long campaignId, CancellationToken ct = default);

    /// <summary>TEAM_012: <paramref name="sort"/> rỗng (Field = null) là thứ tự mặc định.</summary>
    Task<(IReadOnlyList<Campaign> Items, int Total)> SearchAsync(
        CampaignStatus? status, long? assignedTo, int page, int pageSize,
        SortSpec<CampaignSortField> sort, CancellationToken ct = default);

    /// <summary>Tổng chỉ tiêu đã chia cho các task của một chiến dịch.</summary>
    Task<long> SumAllocatedAsync(long campaignId, CancellationToken ct = default);

    /// <summary>Tiến độ từ view v_campaign_progress; null = tất cả chiến dịch.</summary>
    Task<List<CampaignProgress>> GetProgressAsync(
        IReadOnlyCollection<long>? campaignIds, CancellationToken ct = default);

    /// <summary>Kiểm tra user có role TaskManager không.</summary>
    Task<bool> IsTaskManagerAsync(long userId, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class CampaignRepository(CodeSwitchLabelDbContext db) : ICampaignRepository
{
    public void Add(Campaign campaign) => db.Campaigns.Add(campaign);

    public void Remove(Campaign campaign) => db.Campaigns.Remove(campaign);

    public Task<Campaign?> GetForUpdateAsync(long campaignId, CancellationToken ct = default) =>
        db.Campaigns.FirstOrDefaultAsync(c => c.CampaignId == campaignId, ct);

    public Task<Campaign?> GetAsync(long campaignId, CancellationToken ct = default) =>
        db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.CampaignId == campaignId, ct);

    public async Task<(IReadOnlyList<Campaign> Items, int Total)> SearchAsync(
        CampaignStatus? status, long? assignedTo, int page, int pageSize,
        SortSpec<CampaignSortField> sort, CancellationToken ct = default)
    {
        var query = db.Campaigns.AsNoTracking();

        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (assignedTo.HasValue) query = query.Where(c => c.AssignedTo == assignedTo.Value);

        var total = await query.CountAsync(ct);

        // TEAM_012: nhánh cuối là thứ tự cũ (đợt mới nhất trước).
        var ordered = sort.Field switch
        {
            CampaignSortField.CampaignId => sort.Descending
                ? query.OrderByDescending(c => c.CampaignId)
                : query.OrderBy(c => c.CampaignId),
            CampaignSortField.StartDate => sort.Descending
                ? query.OrderByDescending(c => c.StartDate)
                : query.OrderBy(c => c.StartDate),
            CampaignSortField.EndDate => sort.Descending
                ? query.OrderByDescending(c => c.EndDate)
                : query.OrderBy(c => c.EndDate),
            CampaignSortField.Status => sort.Descending
                ? query.OrderByDescending(c => c.Status)
                : query.OrderBy(c => c.Status),
            CampaignSortField.CreatedAt => sort.Descending
                ? query.OrderByDescending(c => c.CreatedAt)
                : query.OrderBy(c => c.CreatedAt),
            _ => query.OrderByDescending(c => c.StartDate)
        };

        var items = await ordered
            .ThenBy(c => c.CampaignId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<long> SumAllocatedAsync(long campaignId, CancellationToken ct = default) =>
        db.WorkTasks.Where(t => t.CampaignId == campaignId)
            .SumAsync(t => (long)t.TargetQty, ct);

    public Task<List<CampaignProgress>> GetProgressAsync(
        IReadOnlyCollection<long>? campaignIds, CancellationToken ct = default)
    {
        var query = db.CampaignProgress.AsNoTracking();

        if (campaignIds is not null)
        {
            var ids = campaignIds.ToList();
            query = query.Where(p => ids.Contains(p.CampaignId));
        }

        return query.OrderByDescending(p => p.StartDate).ThenBy(p => p.CampaignId).ToListAsync(ct);
    }

    public async Task<bool> IsTaskManagerAsync(long userId, CancellationToken ct = default)
    {
        return await db.AppUsers
            .Where(u => u.UserId == userId)
            .Join(db.Roles, u => u.RoleId, r => r.RoleId, (u, r) => r.RoleName)
            .AnyAsync(r => r == RoleName.TaskManager, ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
