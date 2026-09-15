using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<AppUser?> GetByIdAsync(long userId, CancellationToken ct = default);
}

public class UserRepository(CodeSwitchLabelDbContext db) : IUserRepository
{
    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        db.AppUsers
            .Include(u => u.Role)
            .Include(u => u.SpeakerProfile)
            // So sánh không phân biệt hoa thường: người dùng gõ Admin@... hay admin@... đều vào được.
            .FirstOrDefaultAsync(u => EF.Functions.ILike(u.Email, email), ct);

    public Task<AppUser?> GetByIdAsync(long userId, CancellationToken ct = default) =>
        db.AppUsers
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.SpeakerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);
}

public interface ISystemConfigRepository
{
    Task<IReadOnlyList<SystemConfig>> GetAllAsync(CancellationToken ct = default);
    Task<SystemConfig?> GetAsync(string key, CancellationToken ct = default);
    Task<bool> UpdateValueAsync(string key, string value, long updatedById, CancellationToken ct = default);
}

public class SystemConfigRepository(CodeSwitchLabelDbContext db) : ISystemConfigRepository
{
    public async Task<IReadOnlyList<SystemConfig>> GetAllAsync(CancellationToken ct = default) =>
        await db.SystemConfigs.AsNoTracking().OrderBy(c => c.ConfigKey).ToListAsync(ct);

    public Task<SystemConfig?> GetAsync(string key, CancellationToken ct = default) =>
        db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.ConfigKey == key, ct);

    public async Task<bool> UpdateValueAsync(
        string key, string value, long updatedById, CancellationToken ct = default)
    {
        var affected = await db.SystemConfigs
            .Where(c => c.ConfigKey == key)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ConfigValue, value)
                .SetProperty(c => c.UpdatedById, updatedById)
                .SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow), ct);

        return affected > 0;
    }
}

public interface IReasonRepository
{
    Task<IReadOnlyList<RejectionReason>> GetRejectionReasonsAsync(
        bool activeOnly, CancellationToken ct = default);

    Task<IReadOnlyList<ScriptErrorReason>> GetScriptErrorReasonsAsync(
        bool activeOnly, CancellationToken ct = default);

    Task<ScriptErrorReason?> GetScriptErrorReasonByCodeAsync(
        string code, CancellationToken ct = default);
}

public class ReasonRepository(CodeSwitchLabelDbContext db) : IReasonRepository
{
    public async Task<IReadOnlyList<RejectionReason>> GetRejectionReasonsAsync(
        bool activeOnly, CancellationToken ct = default)
    {
        var query = db.RejectionReasons.AsNoTracking();
        if (activeOnly) query = query.Where(r => r.IsActive);

        return await query.OrderBy(r => r.Category).ThenBy(r => r.ReasonCode).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScriptErrorReason>> GetScriptErrorReasonsAsync(
        bool activeOnly, CancellationToken ct = default)
    {
        var query = db.ScriptErrorReasons.AsNoTracking();
        if (activeOnly) query = query.Where(r => r.IsActive);

        return await query.OrderBy(r => r.SortOrder).ToListAsync(ct);
    }

    public Task<ScriptErrorReason?> GetScriptErrorReasonByCodeAsync(
        string code, CancellationToken ct = default) =>
        db.ScriptErrorReasons.AsNoTracking().FirstOrDefaultAsync(r => r.ReasonCode == code, ct);
}
