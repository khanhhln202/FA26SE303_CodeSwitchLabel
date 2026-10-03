using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

/// <summary>
/// Chủ đề Reviewer đủ trình độ duyệt. Dùng ở hai chỗ: Admin phân chủ đề cho Reviewer,
/// và ScriptService kiểm trước khi chuyển một cặp câu sang trạng thái đã duyệt
/// (trigger trg_script_validated_domain của database là lưới chắn cuối).
/// </summary>
public interface IUserDomainRepository
{
    Task<IReadOnlyList<ScriptDomain>> GetDomainsAsync(long userId, CancellationToken ct = default);

    Task<bool> IsQualifiedAsync(long userId, ScriptDomain domain, CancellationToken ct = default);

    /// <summary>Đặt lại toàn bộ chủ đề của một người: chủ đề nào không còn trong danh sách thì bị gỡ.</summary>
    Task ReplaceAsync(long userId, IEnumerable<ScriptDomain> domains, CancellationToken ct = default);
}

public class UserDomainRepository(CodeSwitchLabelDbContext db) : IUserDomainRepository
{
    public async Task<IReadOnlyList<ScriptDomain>> GetDomainsAsync(
        long userId, CancellationToken ct = default) =>
        await db.UserDomains.AsNoTracking()
            .Where(d => d.UserId == userId)
            .Select(d => d.Domain)
            .ToListAsync(ct);

    public Task<bool> IsQualifiedAsync(long userId, ScriptDomain domain, CancellationToken ct = default) =>
        db.UserDomains.AsNoTracking().AnyAsync(d => d.UserId == userId && d.Domain == domain, ct);

    public async Task ReplaceAsync(
        long userId, IEnumerable<ScriptDomain> domains, CancellationToken ct = default)
    {
        var wanted = domains.ToHashSet();

        var existing = await db.UserDomains
            .Where(d => d.UserId == userId)
            .ToListAsync(ct);

        foreach (var row in existing.Where(r => !wanted.Contains(r.Domain)))
        {
            db.UserDomains.Remove(row);
        }

        var current = existing.Select(r => r.Domain).ToHashSet();

        foreach (var domain in wanted.Where(d => !current.Contains(d)))
        {
            db.UserDomains.Add(new UserDomain
            {
                UserId = userId,
                Domain = domain,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
