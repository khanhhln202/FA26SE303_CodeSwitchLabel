using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeSwitchLabel.Repositories.Repositories;

/// <summary>Một task duyệt đang giao cho Reviewer, kèm số bản người đó đã duyệt trong task.</summary>
public record ReviewTaskProgress(
    long TaskId, string? Description, int TargetQty, int Done, DateTimeOffset? Deadline);

/// <summary>
/// Một hàng trong danh sách bản ghi của task duyệt. Chỉ mang con số lượt đã có và cờ
/// "người đang xem đã duyệt chưa" — KHÔNG mang quyết định của ai, vì mọi lượt đều duyệt mù.
/// </summary>
public record TaskReviewRow(
    string RecordingId,
    long SpeakerId,
    string ScriptId,
    SentenceVariant SentenceVariant,
    string ScriptTagged,
    decimal DurationSec,
    RecordingStatus Status,
    TaskRecordingStatus QueueStatus,
    int ReviewsDone,
    bool MyReviewDone);

public interface IReviewRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Đọc và KHOÁ hàng bản ghi tới hết transaction. Hai Reviewer bấm duyệt cùng lúc thì
    /// người sau phải đợi, rồi đọc lại và thấy vòng đó đã có người làm.
    /// Phải gọi bên trong một transaction.
    /// </summary>
    Task<Recording?> LockRecordingAsync(string recordingId, CancellationToken ct = default);

    Task<List<Review>> GetReviewsAsync(string recordingId, CancellationToken ct = default);

    /// <summary>Trạng thái mới nhất dưới database, sau khi trigger chốt theo đa số.</summary>
    Task<RecordingStatus> GetRecordingStatusAsync(string recordingId, CancellationToken ct = default);

    Task<List<RejectionReason>> GetReasonsByCodesAsync(string[] codes, CancellationToken ct = default);

    Task<bool> IsActiveReviewTaskOfAsync(long taskId, long reviewerId, CancellationToken ct = default);

    Task<TaskRecording?> GetQueuedTaskRecordingAsync(
        long taskId, long reviewerId, string recordingId, CancellationToken ct = default);

    /// <summary>
    /// Bản ghi tiếp theo người này được duyệt: còn chờ duyệt, không phải bản của chính mình,
    /// mình chưa duyệt lần nào, và chưa đủ số vòng yêu cầu.
    /// </summary>
    Task<Recording?> GetNextForReviewerAsync(
        long reviewerId, long? taskId, long? speakerId, bool random,
        int roundsRequired, CancellationToken ct = default);

    /// <summary>Một bản ghi kèm câu của nó, để Reviewer mở thẳng bản chọn từ danh sách.</summary>
    Task<Recording?> GetWithScriptAsync(string recordingId, CancellationToken ct = default);

    /// <summary>
    /// Các bản ghi nằm trong một task duyệt, phân trang. Người gọi phải tự kiểm task đó có đang
    /// giao cho người này không — ở đây không kiểm quyền.
    /// </summary>
    Task<(List<TaskReviewRow> Rows, int Total)> GetTaskReviewRowsAsync(
        long taskId, long reviewerId, bool onlyReviewable, int roundsRequired,
        int page, int pageSize, CancellationToken ct = default);

    Task<List<ReviewTaskProgress>> GetActiveTaskProgressAsync(
        long reviewerId, CancellationToken ct = default);

    Task<int> CountReviewsByAsync(long reviewerId, CancellationToken ct = default);

    void Add(Review review);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class ReviewRepository(CodeSwitchLabelDbContext db) : IReviewRepository
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);

    public async Task<Recording?> LockRecordingAsync(string recordingId, CancellationToken ct = default)
    {
        // SQL thô vì LINQ không diễn đạt được FOR UPDATE.
        var rows = await db.Recordings
            .FromSqlInterpolated($"SELECT * FROM recording WHERE recording_id = {recordingId} FOR UPDATE")
            .ToListAsync(ct);

        return rows.FirstOrDefault();
    }

    public Task<List<Review>> GetReviewsAsync(string recordingId, CancellationToken ct = default) =>
        db.Reviews
            .AsNoTracking()
            .Include(v => v.RejectionReasons).ThenInclude(rr => rr.Reason)
            .Where(v => v.RecordingId == recordingId)
            .OrderBy(v => v.ReviewRound)
            .ToListAsync(ct);

    public Task<RecordingStatus> GetRecordingStatusAsync(
        string recordingId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking()
            .Where(r => r.RecordingId == recordingId)
            .Select(r => r.Status)
            .FirstAsync(ct);

    public Task<List<RejectionReason>> GetReasonsByCodesAsync(
        string[] codes, CancellationToken ct = default) =>
        db.RejectionReasons.AsNoTracking().Where(r => codes.Contains(r.ReasonCode)).ToListAsync(ct);

    public Task<bool> IsActiveReviewTaskOfAsync(
        long taskId, long reviewerId, CancellationToken ct = default) =>
        db.WorkTasks.AsNoTracking().AnyAsync(t =>
            t.TaskId == taskId &&
            t.TaskType == TaskType.Review &&
            (t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.InProgress) &&
            t.Assignments.Any(a => a.UserId == reviewerId && a.AssignmentStatus == AssignmentStatus.Active), ct);

    public Task<TaskRecording?> GetQueuedTaskRecordingAsync(
        long taskId, long reviewerId, string recordingId, CancellationToken ct = default) =>
        db.TaskRecordings.FirstOrDefaultAsync(tr =>
            tr.TaskId == taskId &&
            tr.RecordingId == recordingId &&
            tr.Status == TaskRecordingStatus.Queued &&
            tr.Task.TaskType == TaskType.Review &&
            (tr.Task.Status == WorkTaskStatus.Open || tr.Task.Status == WorkTaskStatus.InProgress) &&
            tr.Task.Assignments.Any(a => a.UserId == reviewerId && a.AssignmentStatus == AssignmentStatus.Active), ct);

    public Task<Recording?> GetNextForReviewerAsync(
        long reviewerId, long? taskId, long? speakerId, bool random,
        int roundsRequired, CancellationToken ct = default)
    {
        var query = db.Recordings
            .AsNoTracking()
            .Include(r => r.Script)
            .Where(r => r.Status == RecordingStatus.PendingReview)
            .Where(r => r.SpeakerId != reviewerId)                        // không tự duyệt bản của mình
            .Where(r => !r.Reviews.Any(v => v.ReviewerId == reviewerId))  // mỗi người chỉ duyệt một lần
            .Where(r => r.Reviews.Count < roundsRequired);

        if (speakerId.HasValue) query = query.Where(r => r.SpeakerId == speakerId.Value);

        if (taskId.HasValue)
        {
            query = query.Where(r => r.TaskRecordings.Any(tr =>
                tr.TaskId == taskId.Value && tr.Status == TaskRecordingStatus.Queued));
        }

        // Không chọn ngẫu nhiên thì ưu tiên bản gần đủ vòng nhất rồi tới bản cũ nhất,
        // để bản ghi sớm được chốt thay vì ai cũng bắt đầu một bản mới.
        query = random
            ? query.OrderBy(_ => EF.Functions.Random())
            : query.OrderByDescending(r => r.Reviews.Count).ThenBy(r => r.RecordedAt);

        return query.FirstOrDefaultAsync(ct);
    }

    public Task<Recording?> GetWithScriptAsync(string recordingId, CancellationToken ct = default) =>
        db.Recordings
            .AsNoTracking()
            .Include(r => r.Script)
            .FirstOrDefaultAsync(r => r.RecordingId == recordingId, ct);

    public async Task<(List<TaskReviewRow> Rows, int Total)> GetTaskReviewRowsAsync(
        long taskId, long reviewerId, bool onlyReviewable, int roundsRequired,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.TaskRecordings.AsNoTracking().Where(tr => tr.TaskId == taskId);

        if (onlyReviewable)
        {
            // Giống hệt bộ lọc của GetNextForReviewerAsync, để danh sách và GET next không nói khác nhau.
            query = query
                .Where(tr => tr.Recording.Status == RecordingStatus.PendingReview)
                .Where(tr => tr.Recording.SpeakerId != reviewerId)
                .Where(tr => !tr.Recording.Reviews.Any(v => v.ReviewerId == reviewerId))
                .Where(tr => tr.Recording.Reviews.Count < roundsRequired);
        }

        var total = await query.CountAsync(ct);

        // Cùng thứ tự với GET next: bản gần đủ lượt lên trước, rồi tới bản cũ nhất.
        var rows = await query
            .OrderByDescending(tr => tr.Recording.Reviews.Count)
            .ThenBy(tr => tr.Recording.RecordedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(tr => new TaskReviewRow(
                tr.RecordingId,
                tr.Recording.SpeakerId,
                tr.Recording.ScriptId,
                tr.Recording.SentenceVariant,
                tr.Recording.SentenceVariant == SentenceVariant.CodeSwitching
                    ? tr.Recording.Script.CsContent
                    : tr.Recording.Script.VeContent,
                tr.Recording.DurationSec,
                tr.Recording.Status,
                tr.Status,
                tr.Recording.Reviews.Count,
                tr.Recording.Reviews.Any(v => v.ReviewerId == reviewerId)))
            .ToListAsync(ct);

        return (rows, total);
    }

    public Task<List<ReviewTaskProgress>> GetActiveTaskProgressAsync(
        long reviewerId, CancellationToken ct = default) =>
        db.TaskAssignments
            .AsNoTracking()
            .Where(a => a.UserId == reviewerId && a.AssignmentStatus == AssignmentStatus.Active)
            .Where(a => a.Task.TaskType == TaskType.Review)
            .Where(a => a.Task.Status == WorkTaskStatus.Open || a.Task.Status == WorkTaskStatus.InProgress)
            .OrderBy(a => a.Task.Deadline)
            .Select(a => new ReviewTaskProgress(
                a.TaskId,
                a.Task.Description,
                a.Task.TargetQty,
                db.Reviews.Count(v => v.TaskId == a.TaskId && v.ReviewerId == reviewerId),
                a.Task.Deadline))
            .ToListAsync(ct);

    public Task<int> CountReviewsByAsync(long reviewerId, CancellationToken ct = default) =>
        db.Reviews.AsNoTracking().CountAsync(v => v.ReviewerId == reviewerId, ct);

    public void Add(Review review) => db.Reviews.Add(review);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
