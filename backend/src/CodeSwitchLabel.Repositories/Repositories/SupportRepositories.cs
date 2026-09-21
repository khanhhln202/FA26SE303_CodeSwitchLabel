using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<AppUser?> GetByIdAsync(long userId, CancellationToken ct = default);
    Task<List<AppUser>> GetByRoleAsync(RoleName role, CancellationToken ct = default);
}

public class UserRepository(CodeSwitchLabelDbContext db) : IUserRepository
{
    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Load().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<AppUser?> GetByIdAsync(long userId, CancellationToken ct = default) =>
        Load().FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public Task<List<AppUser>> GetByRoleAsync(RoleName role, CancellationToken ct = default) =>
        Load().Where(u => u.Role.RoleName == role).OrderBy(u => u.FullName).ToListAsync(ct);

    private IQueryable<AppUser> Load() =>
        db.AppUsers.AsNoTracking().Include(u => u.Role).Include(u => u.SpeakerProfile);
}

public interface ISystemConfigRepository
{
    Task<SystemConfig?> GetAsync(string key, CancellationToken ct = default);
    Task<List<SystemConfig>> GetAllAsync(CancellationToken ct = default);
    Task<bool> UpdateValueAsync(string key, string value, long updatedById, CancellationToken ct = default);
}

public class SystemConfigRepository(CodeSwitchLabelDbContext db) : ISystemConfigRepository
{
    public Task<SystemConfig?> GetAsync(string key, CancellationToken ct = default) =>
        db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.ConfigKey == key, ct);

    public Task<List<SystemConfig>> GetAllAsync(CancellationToken ct = default) =>
        db.SystemConfigs.AsNoTracking().OrderBy(c => c.ConfigKey).ToListAsync(ct);

    public async Task<bool> UpdateValueAsync(
        string key, string value, long updatedById, CancellationToken ct = default)
    {
        var config = await db.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == key, ct);
        if (config is null) return false;

        config.ConfigValue = value;
        config.UpdatedBy = updatedById;

        // updated_at do trigger trg_system_config_touch của database tự đặt.
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public interface IReasonRepository
{
    Task<List<RejectionReason>> GetRejectionReasonsAsync(bool activeOnly, CancellationToken ct = default);
    Task<List<ScriptErrorReason>> GetScriptErrorReasonsAsync(bool activeOnly, CancellationToken ct = default);
    Task<ScriptErrorReason?> GetScriptErrorReasonByCodeAsync(string code, CancellationToken ct = default);
}

public class ReasonRepository(CodeSwitchLabelDbContext db) : IReasonRepository
{
    public Task<List<RejectionReason>> GetRejectionReasonsAsync(
        bool activeOnly, CancellationToken ct = default) =>
        db.RejectionReasons.AsNoTracking()
            .Where(r => !activeOnly || r.IsActive)
            .OrderBy(r => r.Category).ThenBy(r => r.ReasonCode)
            .ToListAsync(ct);

    public Task<List<ScriptErrorReason>> GetScriptErrorReasonsAsync(
        bool activeOnly, CancellationToken ct = default) =>
        db.ScriptErrorReasons.AsNoTracking()
            .Where(r => !activeOnly || r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

    public Task<ScriptErrorReason?> GetScriptErrorReasonByCodeAsync(
        string code, CancellationToken ct = default) =>
        db.ScriptErrorReasons.AsNoTracking().FirstOrDefaultAsync(r => r.ReasonCode == code, ct);
}

/// <summary>
/// Đọc bốn view thống kê có sẵn trong lược đồ. Không tự tính lại trong C#:
/// con số trên dashboard và con số trong báo cáo SQL phải là một.
/// </summary>
public interface IStatisticsRepository
{
    Task<DashboardSummary> GetDashboardAsync(CancellationToken ct = default);
    Task<List<SpeakerPerformance>> GetSpeakerPerformanceAsync(CancellationToken ct = default);
    Task<List<ReviewerPerformance>> GetReviewerPerformanceAsync(CancellationToken ct = default);
    Task<List<RejectionReasonStat>> GetRejectionStatsAsync(CancellationToken ct = default);
}

public class StatisticsRepository(CodeSwitchLabelDbContext db) : IStatisticsRepository
{
    public Task<DashboardSummary> GetDashboardAsync(CancellationToken ct = default) =>
        db.DashboardSummary.AsNoTracking().FirstAsync(ct);

    public Task<List<SpeakerPerformance>> GetSpeakerPerformanceAsync(CancellationToken ct = default) =>
        db.SpeakerPerformance.AsNoTracking()
            .OrderByDescending(s => s.Recordings).ThenBy(s => s.FullName)
            .ToListAsync(ct);

    public Task<List<ReviewerPerformance>> GetReviewerPerformanceAsync(CancellationToken ct = default) =>
        db.ReviewerPerformance.AsNoTracking()
            .OrderByDescending(r => r.ReviewsDone).ThenBy(r => r.FullName)
            .ToListAsync(ct);

    public Task<List<RejectionReasonStat>> GetRejectionStatsAsync(CancellationToken ct = default) =>
        db.RejectionReasonStats.AsNoTracking()
            .OrderByDescending(r => r.TimesUsed)
            .ToListAsync(ct);
}
