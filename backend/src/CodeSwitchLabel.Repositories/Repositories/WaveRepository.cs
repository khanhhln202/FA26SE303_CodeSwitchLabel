using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.WorkTasks;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

public interface IWaveRepository
{
    void Add(Wave wave);

    /// <summary>
    /// Xoá chiến dịch. Task gắn vào KHÔNG bị xoá: FK task.wave_id là
    /// ON DELETE SET NULL nên chúng tự thành task độc lập.
    /// </summary>
    void Remove(Wave wave);

    Task<Wave?> GetForUpdateAsync(long waveId, CancellationToken ct = default);
    Task<Wave?> GetAsync(long waveId, CancellationToken ct = default);

    /// <summary><paramref name="sort"/> rỗng (Field = null) là thứ tự mặc định.</summary>
    Task<(IReadOnlyList<Wave> Items, int Total)> SearchAsync(
        WaveStatus? status, long? assignedTo, int page, int pageSize,
        SortSpec<WaveSortField> sort, CancellationToken ct = default);

    /// <summary>Tổng chỉ tiêu đã chia cho các task của một chiến dịch.</summary>
    Task<long> SumAllocatedAsync(long waveId, CancellationToken ct = default);

    /// <summary>Tiến độ từ view v_wave_progress; null = tất cả chiến dịch.</summary>
    Task<List<WaveProgress>> GetProgressAsync(
        IReadOnlyCollection<long>? waveIds, CancellationToken ct = default);

    /// <summary>Kiểm tra user có role TaskManager không.</summary>
    Task<bool> IsTaskManagerAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// tính lại trạng thái đợt từ các task bên trong rồi lưu. Không ai bấm tay
    /// được trạng thái đợt nữa, nên đây là chỗ DUY NHẤT được ghi nó.
    ///
    /// Khoá dòng wave trước khi đếm: hai task cùng đợt đổi trạng thái song song thì
    /// transaction thứ hai phải chờ, rồi mới đếm trên trạng thái mới nhất. Không khoá thì
    /// kết quả là giá trị cũ của một transaction ghi đè lên kết quả của transaction kia.
    /// </summary>
    Task RefreshStatusAsync(long waveId, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class WaveRepository(CodeSwitchLabelDbContext db) : IWaveRepository
{
    public void Add(Wave wave) => db.Waves.Add(wave);

    public void Remove(Wave wave) => db.Waves.Remove(wave);

    /// <summary>
    /// Khoá dòng wave thật sự (<c>FOR UPDATE</c>) để các thao tác create/update/delete
    /// và tạo task thu âm idempotent không race nhau. Dùng <c>SingleOrDefault</c> vì SQL thô
    /// đã lọc theo khoá chính; EF không đọc được WHERE trong câu thô nên <c>First</c> sẽ cảnh báo.
    /// </summary>
    public Task<Wave?> GetForUpdateAsync(long waveId, CancellationToken ct = default) =>
        db.Waves
            .FromSqlInterpolated($"SELECT * FROM wave WHERE wave_id = {waveId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    public Task<Wave?> GetAsync(long waveId, CancellationToken ct = default) =>
        db.Waves.AsNoTracking().FirstOrDefaultAsync(c => c.WaveId == waveId, ct);

    public async Task<(IReadOnlyList<Wave> Items, int Total)> SearchAsync(
        WaveStatus? status, long? assignedTo, int page, int pageSize,
        SortSpec<WaveSortField> sort, CancellationToken ct = default)
    {
        var query = db.Waves.AsNoTracking();

        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (assignedTo.HasValue) query = query.Where(c => c.AssignedTo == assignedTo.Value);

        var total = await query.CountAsync(ct);

        // nhánh cuối là thứ tự cũ (đợt mới nhất trước).
        var ordered = sort.Field switch
        {
            WaveSortField.WaveId => sort.Descending
                ? query.OrderByDescending(c => c.WaveId)
                : query.OrderBy(c => c.WaveId),
            WaveSortField.StartDate => sort.Descending
                ? query.OrderByDescending(c => c.StartDate)
                : query.OrderBy(c => c.StartDate),
            WaveSortField.EndDate => sort.Descending
                ? query.OrderByDescending(c => c.EndDate)
                : query.OrderBy(c => c.EndDate),
            WaveSortField.Status => sort.Descending
                ? query.OrderByDescending(c => c.Status)
                : query.OrderBy(c => c.Status),
            WaveSortField.CreatedAt => sort.Descending
                ? query.OrderByDescending(c => c.CreatedAt)
                : query.OrderBy(c => c.CreatedAt),
            _ => query.OrderByDescending(c => c.StartDate)
        };

        var items = await ordered
            .ThenBy(c => c.WaveId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<long> SumAllocatedAsync(long waveId, CancellationToken ct = default) =>
        db.WorkTasks.Where(t => t.WaveId == waveId)
            .SumAsync(t => (long)t.TargetQty, ct);

    public Task<List<WaveProgress>> GetProgressAsync(
        IReadOnlyCollection<long>? waveIds, CancellationToken ct = default)
    {
        var query = db.WaveProgress.AsNoTracking();

        if (waveIds is not null)
        {
            var ids = waveIds.ToList();
            query = query.Where(p => ids.Contains(p.WaveId));
        }

        return query.OrderByDescending(p => p.StartDate).ThenBy(p => p.WaveId).ToListAsync(ct);
    }

    public async Task<bool> IsTaskManagerAsync(long userId, CancellationToken ct = default)
    {
        return await db.AppUsers
            .Where(u => u.UserId == userId)
            .Join(db.Roles, u => u.RoleId, r => r.RoleId, (u, r) => r.RoleName)
            .AnyAsync(r => r == RoleName.TaskManager, ct);
    }

    public async Task RefreshStatusAsync(long waveId, CancellationToken ct = default)
    {
        // cùng câu FOR UPDATE với GetForUpdateAsync — dùng SingleOrDefault
        // vì SQL thô đã lọc theo khoá chính (First cảnh báo EF).
        var wave = await db.Waves
            .FromSqlInterpolated($"SELECT * FROM wave WHERE wave_id = {waveId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

        if (wave is null) return;

        var taskCount = await db.WorkTasks.CountAsync(t => t.WaveId == waveId, ct);
        var completed = await db.WorkTasks.CountAsync(
            t => t.WaveId == waveId && t.Status == WorkTaskStatus.Completed, ct);

        wave.Status = WaveStateMachine.Evaluate(taskCount, completed);

        await db.SaveChangesAsync(ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
