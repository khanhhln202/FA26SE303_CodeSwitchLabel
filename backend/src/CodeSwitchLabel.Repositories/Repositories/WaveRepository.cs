using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.WorkTasks;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

/// <summary>Một Speaker tham gia chiến dịch, kèm thông tin nhân khẩu cần thống kê.</summary>
public record WaveDashboardSpeaker(
    long UserId, string FullName, Gender? Gender, SpeakerRegion? Region, short? BirthYear);

public record WaveDashboardRow(
    long WaveId,
    string WaveName,
    string Domain,
    string DomainName,
    IReadOnlyList<WaveDashboardSpeaker> Speakers,
    int SentencePairs,
    int ApprovedPairs,
    decimal AudioSeconds);

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
    Task UpdateImportCountsAsync(long waveId, int? bufferQty, int importedDelta, CancellationToken ct = default);
    Task<(int ValidatedTotal, int RecordedPairs)> GetAvailableScriptsAsync(string domain, CancellationToken ct = default);
    Task<int> CountAvailableScriptsAsync(string domain, CancellationToken ct = default);

    /// <summary><paramref name="sort"/> rỗng (Field = null) là thứ tự mặc định.</summary>
    Task<(IReadOnlyList<Wave> Items, int Total)> SearchAsync(
        WaveStatus? status, long? assignedTo, int page, int pageSize,
        SortSpec<WaveSortField> sort, CancellationToken ct = default);

    /// <summary>Tổng chỉ tiêu đã chia cho các task của một chiến dịch.</summary>
    Task<long> SumAllocatedAsync(long waveId, CancellationToken ct = default);

    /// <summary>Tiến độ từ view v_wave_progress; null = tất cả chiến dịch.</summary>
    Task<List<WaveProgress>> GetProgressAsync(
        IReadOnlyCollection<long>? waveIds, CancellationToken ct = default);

    /// <summary>Số liệu tổng hợp của một chiến dịch cho dashboard Task Manager.</summary>
    Task<WaveDashboardRow?> GetDashboardAsync(long waveId, CancellationToken ct = default);

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

    public async Task UpdateImportCountsAsync(long waveId, int? bufferQty, int importedDelta, CancellationToken ct = default)
    {
        var query = db.Waves.Where(w => w.WaveId == waveId);
        if (bufferQty.HasValue)
            await query.ExecuteUpdateAsync(s => s
                .SetProperty(w => w.BufferQty, bufferQty.Value)
                .SetProperty(w => w.ImportedQty, w => w.ImportedQty + importedDelta), ct);
        else if (importedDelta != 0)
            await query.ExecuteUpdateAsync(s => s.SetProperty(w => w.ImportedQty, w => w.ImportedQty + importedDelta), ct);
    }

    public async Task<(int ValidatedTotal, int RecordedPairs)> GetAvailableScriptsAsync(
        string domain, CancellationToken ct = default)
    {
        var scripts = db.Scripts.AsNoTracking()
            .Where(s => s.Domain == domain && s.Status == ScriptStatus.Validated);
        var total = await scripts.CountAsync(ct);
        var recorded = await scripts.CountAsync(s => s.Recordings.Any(r =>
            r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved), ct);
        return (total, recorded);
    }

    public Task<int> CountAvailableScriptsAsync(string domain, CancellationToken ct = default) =>
        db.Scripts.AsNoTracking().CountAsync(s =>
            s.Domain == domain &&
            s.Status == ScriptStatus.Validated &&
            s.WaveId == null &&
            !s.TaskScripts.Any(ts =>
                ts.Status == TaskScriptStatus.Pending && ts.Task.Status != WorkTaskStatus.Cancelled) &&
            !s.Recordings.Any(r =>
                r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved), ct);

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

    public async Task<WaveDashboardRow?> GetDashboardAsync(
        long waveId, CancellationToken ct = default)
    {
        var wave = await db.Waves.AsNoTracking().FirstOrDefaultAsync(w => w.WaveId == waveId, ct);
        if (wave is null) return null;

        var domainName = await db.Domains.AsNoTracking()
            .Where(d => d.Code == wave.Domain)
            .Select(d => d.Name)
            .FirstOrDefaultAsync(ct) ?? wave.Domain;

        var taskIds = await db.WorkTasks.AsNoTracking()
            .Where(t => t.WaveId == waveId)
            .Select(t => t.TaskId)
            .ToListAsync(ct);

        var speakerIds = await db.TaskAssignments.AsNoTracking()
            .Where(a => taskIds.Contains(a.TaskId) &&
                        a.AssignmentStatus != AssignmentStatus.Reassigned &&
                        a.AssignmentStatus != AssignmentStatus.Cancelled)
            .Select(a => a.UserId)
            .Union(db.Recordings.AsNoTracking()
                .Where(r => r.Task != null && r.Task.WaveId == waveId)
                .Select(r => r.SpeakerId))
            .ToListAsync(ct);

        var speakers = await db.AppUsers.AsNoTracking()
            .Where(u => speakerIds.Contains(u.UserId))
            .Select(u => new WaveDashboardSpeaker(
                u.UserId, u.FullName,
                u.SpeakerProfile == null ? null : u.SpeakerProfile.Gender,
                u.SpeakerProfile == null ? null : u.SpeakerProfile.Region,
                u.SpeakerProfile == null ? null : u.SpeakerProfile.BirthYear))
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        var sentencePairs = await db.TaskScripts.AsNoTracking()
            .Where(ts => taskIds.Contains(ts.TaskId))
            .Select(ts => ts.ScriptId)
            .Distinct()
            .CountAsync(ct);

        var approved = await db.Recordings.AsNoTracking()
            .Where(r => r.Task != null && r.Task.WaveId == waveId && r.Status == RecordingStatus.Approved)
            .Select(r => new { r.SpeakerId, r.ScriptId, r.SentenceVariant, r.DurationSec })
            .ToListAsync(ct);

        var approvedPairs = approved
            .GroupBy(r => new { r.SpeakerId, r.ScriptId })
            .Count(g => g.Any(r => r.SentenceVariant == SentenceVariant.CodeSwitching) &&
                        g.Any(r => r.SentenceVariant == SentenceVariant.PureVietnamese));

        return new WaveDashboardRow(
            wave.WaveId, wave.WaveName, wave.Domain, domainName, speakers,
            sentencePairs, approvedPairs, approved.Sum(r => r.DurationSec));
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
