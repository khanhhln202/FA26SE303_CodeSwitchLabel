using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.WorkTasks;
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

    /// <summary><paramref name="sort"/> rỗng (Field = null) là thứ tự mặc định.</summary>
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

    /// <summary>
    /// tính lại trạng thái đợt từ các task bên trong rồi lưu. Không ai bấm tay
    /// được trạng thái đợt nữa, nên đây là chỗ DUY NHẤT được ghi nó.
    ///
    /// Khoá dòng campaign trước khi đếm: hai task cùng đợt đổi trạng thái song song thì
    /// transaction thứ hai phải chờ, rồi mới đếm trên trạng thái mới nhất. Không khoá thì
    /// kết quả là giá trị cũ của một transaction ghi đè lên kết quả của transaction kia.
    /// </summary>
    Task RefreshStatusAsync(long campaignId, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class CampaignRepository(CodeSwitchLabelDbContext db) : ICampaignRepository
{
    public void Add(Campaign campaign) => db.Campaigns.Add(campaign);

    public void Remove(Campaign campaign) => db.Campaigns.Remove(campaign);

    /// <summary>
    /// Khoá dòng campaign thật sự (<c>FOR UPDATE</c>) để các thao tác create/update/delete
    /// và tạo task thu âm idempotent không race nhau. Dùng <c>SingleOrDefault</c> vì SQL thô
    /// đã lọc theo khoá chính; EF không đọc được WHERE trong câu thô nên <c>First</c> sẽ cảnh báo.
    /// </summary>
    public Task<Campaign?> GetForUpdateAsync(long campaignId, CancellationToken ct = default) =>
        db.Campaigns
            .FromSqlInterpolated($"SELECT * FROM campaign WHERE campaign_id = {campaignId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

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

        // nhánh cuối là thứ tự cũ (đợt mới nhất trước).
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

    public async Task RefreshStatusAsync(long campaignId, CancellationToken ct = default)
    {
        // cùng câu FOR UPDATE với GetForUpdateAsync — dùng SingleOrDefault
        // vì SQL thô đã lọc theo khoá chính (First cảnh báo EF).
        var campaign = await db.Campaigns
            .FromSqlInterpolated($"SELECT * FROM campaign WHERE campaign_id = {campaignId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

        if (campaign is null) return;

        var taskCount = await db.WorkTasks.CountAsync(t => t.CampaignId == campaignId, ct);
        var completed = await db.WorkTasks.CountAsync(
            t => t.CampaignId == campaignId && t.Status == WorkTaskStatus.Completed, ct);

        campaign.Status = CampaignStateMachine.Evaluate(taskCount, completed);

        await db.SaveChangesAsync(ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
