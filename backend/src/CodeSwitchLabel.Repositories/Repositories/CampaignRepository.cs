using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

public interface ICampaignRepository
{
    void Add(Campaign campaign);

    Task<Campaign?> GetForUpdateAsync(long campaignId, CancellationToken ct = default);
    Task<Campaign?> GetAsync(long campaignId, CancellationToken ct = default);

    Task<(IReadOnlyList<Campaign> Items, int Total)> SearchAsync(
        CampaignStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Tổng chỉ tiêu đã chia cho các task của một chiến dịch.</summary>
    Task<long> SumAllocatedAsync(long campaignId, CancellationToken ct = default);

    /// <summary>Tiến độ từ view v_campaign_progress; null = tất cả chiến dịch.</summary>
    Task<List<CampaignProgress>> GetProgressAsync(
        IReadOnlyCollection<long>? campaignIds, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class CampaignRepository(CodeSwitchLabelDbContext db) : ICampaignRepository
{
    public void Add(Campaign campaign) => db.Campaigns.Add(campaign);

    public Task<Campaign?> GetForUpdateAsync(long campaignId, CancellationToken ct = default) =>
        db.Campaigns.FirstOrDefaultAsync(c => c.CampaignId == campaignId, ct);

    public Task<Campaign?> GetAsync(long campaignId, CancellationToken ct = default) =>
        db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.CampaignId == campaignId, ct);

    public async Task<(IReadOnlyList<Campaign> Items, int Total)> SearchAsync(
        CampaignStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Campaigns.AsNoTracking();

        if (status.HasValue) query = query.Where(c => c.Status == status.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.StartDate).ThenBy(c => c.CampaignId)
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

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
