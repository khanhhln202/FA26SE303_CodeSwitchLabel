using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeSwitchLabel.Repositories.Repositories;

/// <summary>Một task duyệt đang giao cho Reviewer, kèm số bản người đó đã duyệt trong task.</summary>
public record ReviewTaskProgress(long TaskId, string? Description, int TargetQty, int Done, DateTimeOffset Deadline);

public interface IReviewRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Đọc bản ghi và KHOÁ hàng đó tới hết transaction (SELECT ... FOR UPDATE).
    /// Reviewer thứ hai bấm duyệt đúng cùng lúc sẽ phải đợi người đầu xong,
    /// rồi đọc lại và thấy vòng đó đã có quyết định.
    /// PHẢI gọi bên trong một transaction — gọi ngoài transaction thì khoá nhả ngay khi câu lệnh xong.
    /// </summary>
    Task<Recording?> LockRecordingAsync(long recordingId, CancellationToken ct = default);

    Task<List<Review>> GetReviewsAsync(long recordingId, CancellationToken ct = default);

    Task<List<RejectionReason>> GetReasonsByCodesAsync(string[] codes, CancellationToken ct = default);

    Task<bool> IsActiveReviewTaskOfAsync(long taskId, long reviewerId, CancellationToken ct = default);

    /// <summary>
    /// Dòng task_recording nếu task là task duyệt đang chạy, đang giao cho Reviewer này,
    /// và bản ghi còn nằm chờ trong task. Có theo dõi thay đổi để cập nhật trạng thái.
    /// </summary>
    Task<TaskRecording?> GetQueuedTaskRecordingAsync(
        long taskId, long reviewerId, long recordingId, CancellationToken ct = default);

    Task<Recording?> GetNextForReviewerAsync(
        long reviewerId, long? taskId, long? speakerId, bool random, CancellationToken ct = default);

    Task<List<ReviewTaskProgress>> GetActiveTaskProgressAsync(long reviewerId, CancellationToken ct = default);

    Task<int> CountReviewsByAsync(long reviewerId, CancellationToken ct = default);

    void Add(Review review);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class ReviewRepository(CodeSwitchLabelDbContext db) : IReviewRepository
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);

    public async Task<Recording?> LockRecordingAsync(long recordingId, CancellationToken ct = default)
    {
        // SQL thô vì LINQ không diễn đạt được FOR UPDATE.
        // Gọi ToListAsync thẳng lên câu SQL để EF chạy nguyên văn, không bọc thêm truy vấn con.
        var rows = await db.Recordings
            .FromSqlInterpolated($"SELECT * FROM recording WHERE recording_id = {recordingId} FOR UPDATE")
            .ToListAsync(ct);

        return rows.FirstOrDefault();
    }

    public Task<List<Review>> GetReviewsAsync(long recordingId, CancellationToken ct = default) =>
        db.Reviews
            .AsNoTracking()
            .Include(v => v.RejectionReasons).ThenInclude(rr => rr.Reason)
            .Where(v => v.RecordingId == recordingId)
            .OrderBy(v => v.ReviewRound)
            .ToListAsync(ct);

    public Task<List<RejectionReason>> GetReasonsByCodesAsync(string[] codes, CancellationToken ct = default) =>
        db.RejectionReasons.AsNoTracking().Where(r => codes.Contains(r.ReasonCode)).ToListAsync(ct);

    public Task<bool> IsActiveReviewTaskOfAsync(long taskId, long reviewerId, CancellationToken ct = default) =>
        db.WorkTasks.AsNoTracking().AnyAsync(t =>
            t.TaskId == taskId &&
            t.TaskType == TaskType.Review &&
            (t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.InProgress) &&
            t.Assignments.Any(a => a.UserId == reviewerId && a.AssignmentStatus == AssignmentStatus.Active), ct);

    public Task<TaskRecording?> GetQueuedTaskRecordingAsync(
        long taskId, long reviewerId, long recordingId, CancellationToken ct = default) =>
        db.TaskRecordings.FirstOrDefaultAsync(tr =>
            tr.TaskId == taskId &&
            tr.RecordingId == recordingId &&
            tr.Status == TaskRecordingStatus.Queued &&
            tr.Task.TaskType == TaskType.Review &&
            (tr.Task.Status == WorkTaskStatus.Open || tr.Task.Status == WorkTaskStatus.InProgress) &&
            tr.Task.Assignments.Any(a => a.UserId == reviewerId && a.AssignmentStatus == AssignmentStatus.Active), ct);

    public Task<Recording?> GetNextForReviewerAsync(
        long reviewerId, long? taskId, long? speakerId, bool random, CancellationToken ct = default)
    {
        var query = db.Recordings
            .AsNoTracking()
            .Include(r => r.Script)
            .Include(r => r.Reviews).ThenInclude(v => v.RejectionReasons).ThenInclude(rr => rr.Reason)
            .Where(r => r.Status == RecordingStatus.PendingReview)
            .Where(r => r.SpeakerId != reviewerId)                        // không tự duyệt bản của mình
            .Where(r => !r.Reviews.Any(v => v.ReviewerId == reviewerId))  // mỗi vòng một người khác
            .Where(r => r.Reviews.Count < 3);

        if (speakerId.HasValue)
        {
            query = query.Where(r => r.SpeakerId == speakerId.Value);
        }

        if (taskId.HasValue)
        {
            // Trong task chỉ có vòng 1. Vòng kiểm tra và vòng phân xử nằm ngoài task —
            // ERD ghi rõ task_id NULL nghĩa là lượt kiểm tra ngẫu nhiên.
            query = query.Where(r =>
                r.Reviews.Count == 0 &&
                r.TaskRecordings.Any(tr => tr.TaskId == taskId.Value && tr.Status == TaskRecordingStatus.Queued));
        }

        // Không ngẫu nhiên thì ưu tiên bản đang chờ phân xử, rồi chờ kiểm tra, rồi bản cũ nhất —
        // làm xong phần dang dở trước khi nhận việc mới.
        query = random
            ? query.OrderBy(_ => EF.Functions.Random())
            : query.OrderByDescending(r => r.Reviews.Count).ThenBy(r => r.RecordedAt);

        return query.FirstOrDefaultAsync(ct);
    }

    public Task<List<ReviewTaskProgress>> GetActiveTaskProgressAsync(long reviewerId, CancellationToken ct = default) =>
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
