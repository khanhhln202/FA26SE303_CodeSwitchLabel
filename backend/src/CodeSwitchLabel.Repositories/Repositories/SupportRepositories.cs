using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

/// <summary>Đủ để quyết định một token còn dùng được không: tài khoản còn hoạt động và vai chưa đổi.</summary>
public record UserAuthState(UserStatus Status, RoleName Role);

/// <summary>Task chưa xong mà người dùng đang là người nhận.</summary>
public record UserTaskRow(long TaskId, TaskType TaskType, string? Description, WorkTaskStatus Status);

public interface IUserRepository
{
    /// <summary>So email không phân biệt hoa thường: A@x.com và a@x.com là một người.</summary>
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task<AppUser?> GetByIdAsync(long userId, CancellationToken ct = default);

    /// <summary>Có theo dõi thay đổi, kèm vai và hồ sơ người đọc — dùng khi sửa tài khoản.</summary>
    Task<AppUser?> GetForUpdateAsync(long userId, CancellationToken ct = default);

    Task<List<AppUser>> GetByRoleAsync(RoleName role, CancellationToken ct = default);

    Task<(IReadOnlyList<AppUser> Items, int Total)> SearchAsync(
        RoleName? role, UserStatus? status, string? keyword,
        int page, int pageSize, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    /// <summary>Gọi ở MỖI request đã đăng nhập, nên chỉ đọc đúng hai cột theo khoá chính.</summary>
    Task<UserAuthState?> GetAuthStateAsync(long userId, CancellationToken ct = default);

    Task<int> CountActiveAdminsAsync(CancellationToken ct = default);

    Task<List<UserTaskRow>> GetOpenTasksAsync(long userId, CancellationToken ct = default);

    Task<short> GetRoleIdAsync(RoleName role, CancellationToken ct = default);

    void Add(AppUser user);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class UserRepository(CodeSwitchLabelDbContext db) : IUserRepository
{
    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return Load().FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, ct);
    }

    public Task<AppUser?> GetByIdAsync(long userId, CancellationToken ct = default) =>
        Load().FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public Task<AppUser?> GetForUpdateAsync(long userId, CancellationToken ct = default) =>
        db.AppUsers
            .Include(u => u.Role)
            .Include(u => u.SpeakerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public Task<List<AppUser>> GetByRoleAsync(RoleName role, CancellationToken ct = default) =>
        Load().Where(u => u.Role.RoleName == role).OrderBy(u => u.FullName).ToListAsync(ct);

    public async Task<(IReadOnlyList<AppUser> Items, int Total)> SearchAsync(
        RoleName? role, UserStatus? status, string? keyword,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = Load();

        if (role.HasValue) query = query.Where(u => u.Role.RoleName == role.Value);
        if (status.HasValue) query = query.Where(u => u.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var needle = $"%{keyword.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.FullName, needle) || EF.Functions.ILike(u.Email, needle));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return db.AppUsers.AsNoTracking().AnyAsync(u => u.Email.ToLower() == normalized, ct);
    }

    public Task<UserAuthState?> GetAuthStateAsync(long userId, CancellationToken ct = default) =>
        db.AppUsers.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new UserAuthState(u.Status, u.Role.RoleName))
            .FirstOrDefaultAsync(ct);

    public Task<int> CountActiveAdminsAsync(CancellationToken ct = default) =>
        db.AppUsers.AsNoTracking()
            .CountAsync(u => u.Status == UserStatus.Active && u.Role.RoleName == RoleName.Admin, ct);

    public Task<List<UserTaskRow>> GetOpenTasksAsync(long userId, CancellationToken ct = default) =>
        db.TaskAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.AssignmentStatus == AssignmentStatus.Active)
            .Where(a => a.Task.Status == WorkTaskStatus.Draft ||
                        a.Task.Status == WorkTaskStatus.Open ||
                        a.Task.Status == WorkTaskStatus.InProgress)
            .OrderBy(a => a.TaskId)
            .Select(a => new UserTaskRow(a.TaskId, a.Task.TaskType, a.Task.Description, a.Task.Status))
            .ToListAsync(ct);

    public Task<short> GetRoleIdAsync(RoleName role, CancellationToken ct = default) =>
        db.Roles.AsNoTracking().Where(r => r.RoleName == role).Select(r => r.RoleId).FirstAsync(ct);

    public void Add(AppUser user) => db.AppUsers.Add(user);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

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
