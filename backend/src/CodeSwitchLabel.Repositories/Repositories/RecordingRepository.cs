using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeSwitchLabel.Repositories.Repositories;

public interface IRecordingRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    Task<Recording?> GetAsync(string recordingId, CancellationToken ct = default);

    Task<(IReadOnlyList<Recording> Items, int Total)> SearchAsync(
        long? speakerId, string? scriptId, RecordingStatus? status,
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Người đã thu cặp câu này, nếu có. Lược đồ chỉ cho phép MỘT người đọc cho mỗi cặp câu,
    /// và có trigger chặn ở database.
    /// </summary>
    Task<long?> GetOwnerSpeakerIdAsync(string scriptId, CancellationToken ct = default);

    /// <summary>Đã có bản đang chờ duyệt hoặc đã đạt cho đúng biến thể này chưa.</summary>
    Task<bool> HasActiveRecordingAsync(
        string scriptId, SentenceVariant variant, CancellationToken ct = default);

    /// <summary>Số lần đã thu cho (cặp câu, biến thể) này — lần thu mới là số này cộng một.</summary>
    Task<int> CountTakesAsync(string scriptId, SentenceVariant variant, CancellationToken ct = default);

    /// <summary>Gọi hàm fn_generate_recording_id của database.</summary>
    Task<string> GenerateIdAsync(
        string scriptId, SentenceVariant variant, int take, CancellationToken ct = default);

    /// <summary>Task có đúng là task thu âm đang chạy, đang giao cho người này, và chứa cặp câu này.</summary>
    Task<bool> IsRecordableInTaskAsync(
        long taskId, long speakerId, string scriptId, CancellationToken ct = default);

    /// <summary>Task thu âm đang giao cho người này mà còn chứa cặp câu này ở phần chưa xong.</summary>
    Task<long?> FindOwnTaskContainingScriptAsync(
        long speakerId, string scriptId, CancellationToken ct = default);

    void Add(Recording recording);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class RecordingRepository(CodeSwitchLabelDbContext db) : IRecordingRepository
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);

    public Task<Recording?> GetAsync(string recordingId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking().FirstOrDefaultAsync(r => r.RecordingId == recordingId, ct);

    public async Task<(IReadOnlyList<Recording> Items, int Total)> SearchAsync(
        long? speakerId, string? scriptId, RecordingStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Recordings.AsNoTracking();

        if (speakerId.HasValue) query = query.Where(r => r.SpeakerId == speakerId.Value);
        if (!string.IsNullOrWhiteSpace(scriptId)) query = query.Where(r => r.ScriptId == scriptId);
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.RecordedAt).ThenBy(r => r.RecordingId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<long?> GetOwnerSpeakerIdAsync(string scriptId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking()
            .Where(r => r.ScriptId == scriptId && r.Status != RecordingStatus.QcFailed)
            .Select(r => (long?)r.SpeakerId)
            .FirstOrDefaultAsync(ct);

    public Task<bool> HasActiveRecordingAsync(
        string scriptId, SentenceVariant variant, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking().AnyAsync(r =>
            r.ScriptId == scriptId &&
            r.SentenceVariant == variant &&
            (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved), ct);

    public Task<int> CountTakesAsync(
        string scriptId, SentenceVariant variant, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking()
            .CountAsync(r => r.ScriptId == scriptId && r.SentenceVariant == variant, ct);

    public async Task<string> GenerateIdAsync(
        string scriptId, SentenceVariant variant, int take, CancellationToken ct = default)
    {
        var variantName = SnakeCaseNaming.ToSnakeCase(variant.ToString());

        return await db.Database
            .SqlQuery<string>(
                $"""SELECT fn_generate_recording_id({scriptId}, CAST({variantName} AS sentence_variant), {take}) AS "Value" """)
            .SingleAsync(ct);
    }

    public Task<bool> IsRecordableInTaskAsync(
        long taskId, long speakerId, string scriptId, CancellationToken ct = default) =>
        db.WorkTasks.AsNoTracking().AnyAsync(t =>
            t.TaskId == taskId &&
            t.TaskType == TaskType.Recording &&
            (t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.InProgress) &&
            t.Assignments.Any(a => a.UserId == speakerId && a.AssignmentStatus == AssignmentStatus.Active) &&
            t.TaskScripts.Any(ts => ts.ScriptId == scriptId && ts.Status == TaskScriptStatus.Pending), ct);

    public Task<long?> FindOwnTaskContainingScriptAsync(
        long speakerId, string scriptId, CancellationToken ct = default) =>
        db.WorkTasks
            .AsNoTracking()
            .Where(t => t.TaskType == TaskType.Recording)
            .Where(t => t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.InProgress)
            .Where(t => t.Assignments.Any(a => a.UserId == speakerId && a.AssignmentStatus == AssignmentStatus.Active))
            .Where(t => t.TaskScripts.Any(ts => ts.ScriptId == scriptId && ts.Status == TaskScriptStatus.Pending))
            .OrderBy(t => t.Deadline ?? DateTimeOffset.MaxValue)
            .Select(t => (long?)t.TaskId)
            .FirstOrDefaultAsync(ct);

    public void Add(Recording recording) => db.Recordings.Add(recording);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
