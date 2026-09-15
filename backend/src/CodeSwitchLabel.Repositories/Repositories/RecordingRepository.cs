using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

public interface IRecordingRepository
{
    Task<Recording?> GetAsync(long recordingId, CancellationToken ct = default);

    Task<(IReadOnlyList<Recording> Items, int Total)> SearchForSpeakerAsync(
        long speakerId, RecordingStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Người này đã có bản ghi đang chờ duyệt hoặc đã được duyệt cho script này chưa.
    /// Bản bị từ chối hay trượt kiểm tra tự động thì không tính — vẫn được thu lại.
    /// </summary>
    Task<bool> HasActiveRecordingAsync(long scriptId, long speakerId, CancellationToken ct = default);

    /// <summary>
    /// Task có đúng là task thu âm, còn đang chạy, đang giao cho người này,
    /// và có chứa script này trong phần chưa làm hay không.
    /// </summary>
    Task<bool> IsRecordableInTaskAsync(
        long taskId, long speakerId, long scriptId, CancellationToken ct = default);

    void Add(Recording recording);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class RecordingRepository(CodeSwitchLabelDbContext db) : IRecordingRepository
{
    public Task<Recording?> GetAsync(long recordingId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking().FirstOrDefaultAsync(r => r.RecordingId == recordingId, ct);

    public async Task<(IReadOnlyList<Recording> Items, int Total)> SearchForSpeakerAsync(
        long speakerId, RecordingStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Recordings.AsNoTracking().Where(r => r.SpeakerId == speakerId);

        if (status.HasValue) query = query.Where(r => r.Status == status.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.RecordedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> HasActiveRecordingAsync(long scriptId, long speakerId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking().AnyAsync(r =>
            r.ScriptId == scriptId &&
            r.SpeakerId == speakerId &&
            (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved), ct);

    public Task<bool> IsRecordableInTaskAsync(
        long taskId, long speakerId, long scriptId, CancellationToken ct = default) =>
        db.WorkTasks.AsNoTracking().AnyAsync(t =>
            t.TaskId == taskId &&
            t.TaskType == TaskType.Recording &&
            (t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.InProgress) &&
            t.Assignments.Any(a => a.UserId == speakerId && a.AssignmentStatus == AssignmentStatus.Active) &&
            t.TaskScripts.Any(ts => ts.ScriptId == scriptId && ts.Status == TaskScriptStatus.Pending), ct);

    public void Add(Recording recording) => db.Recordings.Add(recording);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
