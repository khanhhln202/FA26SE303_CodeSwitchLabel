using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeSwitchLabel.Repositories.Repositories;

/// <summary>
/// Một task kèm người đang nhận và các con số tiến độ, tính trong MỘT câu truy vấn.
/// </summary>
/// <param name="Started">Task thu âm: số bản ghi đã nộp, không tính bản trượt QC. Task duyệt: số lượt đã duyệt.</param>
/// <param name="Done">Task thu âm: số CẶP CÂU đã đủ hai bản duyệt đạt. Task duyệt: số bản ghi đã duyệt.</param>
/// <param name="UsableItems">Số mục còn làm được — không tính mục đã bị loại.</param>
public record TaskRow(
    long TaskId,
    TaskType TaskType,
    string? Description,
    WorkTaskStatus Status,
    int TargetQty,
    DateTimeOffset? Deadline,
    DateTimeOffset CreatedAt,
    long? AssigneeId,
    string? AssigneeName,
    int Started,
    int Done,
    int UsableItems,
    int TotalItems);

public record AssigneeSummaryRow(
    long UserId, string FullName, RoleName Role, int ActiveTasks, int TotalTarget, int TotalDone, int OverdueTasks);

public interface ITaskRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    void Add(WorkTask task);
    void AddAssignment(TaskAssignment assignment);
    void AddScriptItems(IEnumerable<TaskScript> items);
    void AddRecordingItems(IEnumerable<TaskRecording> items);
    void Remove(TaskScript item);
    void Remove(TaskRecording item);

    Task<WorkTask?> GetForUpdateAsync(long taskId, CancellationToken ct = default);
    Task<TaskAssignment?> GetActiveAssignmentAsync(long taskId, CancellationToken ct = default);
    Task<List<TaskAssignment>> GetAssignmentHistoryAsync(long taskId, CancellationToken ct = default);

    Task<TaskRow?> GetRowAsync(long taskId, CancellationToken ct = default);

    Task<(IReadOnlyList<TaskRow> Items, int Total)> SearchAsync(
        TaskType? type, WorkTaskStatus? status, long? assigneeId, bool? overdue,
        DateTimeOffset now, int page, int pageSize, CancellationToken ct = default);

    Task<List<TaskRow>> GetActiveRowsForAssigneeAsync(long userId, TaskType type, CancellationToken ct = default);
    Task<List<AssigneeSummaryRow>> GetAssigneeSummaryAsync(DateTimeOffset now, CancellationToken ct = default);

    Task<List<TaskScript>> GetScriptItemsAsync(long taskId, CancellationToken ct = default);
    Task<List<TaskRecording>> GetRecordingItemsAsync(long taskId, CancellationToken ct = default);
    Task<TaskScript?> GetScriptItemAsync(long taskId, string scriptId, CancellationToken ct = default);
    Task<TaskRecording?> GetRecordingItemAsync(long taskId, string recordingId, CancellationToken ct = default);
    Task<List<TaskRecording>> GetQueuedRecordingItemsAsync(string recordingId, CancellationToken ct = default);

    /// <summary>Mọi mục còn chờ của một cặp câu, ở bất kỳ task nào. Có theo dõi thay đổi.</summary>
    Task<List<TaskScript>> GetPendingScriptItemsAsync(string scriptId, CancellationToken ct = default);

    Task<AppUser?> GetUserWithRoleAsync(long userId, CancellationToken ct = default);
    Task<bool> IsActiveTaskOfAsync(long taskId, long userId, TaskType type, CancellationToken ct = default);

    Task<List<Script>> GetScriptsAsync(string[] ids, CancellationToken ct = default);
    Task<List<Recording>> GetRecordingsWithReviewsAsync(string[] ids, CancellationToken ct = default);

    Task<List<string>> FindScriptCandidatesAsync(
        long taskId, long? assigneeId, ScriptDomain? domain, int count, CancellationToken ct = default);

    Task<List<string>> FindRecordingCandidatesAsync(
        long taskId, long? assigneeId, long? speakerId, int count,
        int roundsRequired, CancellationToken ct = default);

    /// <summary>Mục còn chờ trong task mà người này KHÔNG làm được nữa.</summary>
    Task<List<string>> GetBlockedItemIdsAsync(
        long taskId, long userId, TaskType type, CancellationToken ct = default);

    /// <summary>Cặp câu đã có bản ghi nộp — đang chờ duyệt hoặc đã đạt.</summary>
    Task<bool> HasSubmittedRecordingAsync(string scriptId, CancellationToken ct = default);

    /// <summary>Đủ hai bản cs và vi đã duyệt đạt thì cặp câu coi như xong.</summary>
    Task<bool> IsScriptFullyApprovedAsync(string scriptId, CancellationToken ct = default);

    Task<(int Submitted, int Approved)> GetSpeakerTotalsAsync(long speakerId, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class TaskRepository(CodeSwitchLabelDbContext db) : ITaskRepository
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);

    public void Add(WorkTask task) => db.WorkTasks.Add(task);
    public void AddAssignment(TaskAssignment assignment) => db.TaskAssignments.Add(assignment);
    public void AddScriptItems(IEnumerable<TaskScript> items) => db.TaskScripts.AddRange(items);
    public void AddRecordingItems(IEnumerable<TaskRecording> items) => db.TaskRecordings.AddRange(items);
    public void Remove(TaskScript item) => db.TaskScripts.Remove(item);
    public void Remove(TaskRecording item) => db.TaskRecordings.Remove(item);

    public Task<WorkTask?> GetForUpdateAsync(long taskId, CancellationToken ct = default) =>
        db.WorkTasks.FirstOrDefaultAsync(t => t.TaskId == taskId, ct);

    public Task<TaskAssignment?> GetActiveAssignmentAsync(long taskId, CancellationToken ct = default) =>
        db.TaskAssignments.FirstOrDefaultAsync(
            a => a.TaskId == taskId && a.AssignmentStatus == AssignmentStatus.Active, ct);

    public Task<List<TaskAssignment>> GetAssignmentHistoryAsync(long taskId, CancellationToken ct = default) =>
        db.TaskAssignments
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => a.AssignedAt)
            .ToListAsync(ct);

    public Task<TaskRow?> GetRowAsync(long taskId, CancellationToken ct = default) =>
        ProjectRows(db.WorkTasks.AsNoTracking().Where(t => t.TaskId == taskId)).FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<TaskRow> Items, int Total)> SearchAsync(
        TaskType? type, WorkTaskStatus? status, long? assigneeId, bool? overdue,
        DateTimeOffset now, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.WorkTasks.AsNoTracking();

        if (type.HasValue) query = query.Where(t => t.TaskType == type.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);

        if (assigneeId.HasValue)
        {
            query = query.Where(t => t.Assignments.Any(a =>
                a.UserId == assigneeId.Value && a.AssignmentStatus == AssignmentStatus.Active));
        }

        // Quá hạn chỉ có nghĩa với task còn đang làm và có đặt hạn.
        if (overdue == true)
        {
            query = query.Where(t => t.Deadline != null && t.Deadline < now &&
                (t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.InProgress));
        }
        else if (overdue == false)
        {
            query = query.Where(t => !(t.Deadline != null && t.Deadline < now &&
                (t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.InProgress)));
        }

        var total = await query.CountAsync(ct);

        // Lọc, sắp xếp, phân trang TRƯỚC khi chiếu sang TaskRow.
        var items = await ProjectRows(query
                .OrderBy(t => t.Deadline == null)
                .ThenBy(t => t.Deadline)
                .ThenBy(t => t.TaskId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<List<TaskRow>> GetActiveRowsForAssigneeAsync(
        long userId, TaskType type, CancellationToken ct = default) =>
        ProjectRows(db.WorkTasks
                .AsNoTracking()
                .Where(t => t.TaskType == type)
                .Where(t => t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.InProgress)
                .Where(t => t.Assignments.Any(a => a.UserId == userId && a.AssignmentStatus == AssignmentStatus.Active))
                .OrderBy(t => t.Deadline))
            .ToListAsync(ct);

    public async Task<List<AssigneeSummaryRow>> GetAssigneeSummaryAsync(
        DateTimeOffset now, CancellationToken ct = default)
    {
        // Gom trong bộ nhớ: số task đang chạy nhỏ, còn tổng của truy vấn con lồng trong GROUP BY
        // thì EF dịch không ổn định.
        var rows = await ProjectRows(db.WorkTasks
                .AsNoTracking()
                .Where(t => t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.InProgress)
                .Where(t => t.Assignments.Any(a => a.AssignmentStatus == AssignmentStatus.Active)))
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.AssigneeId!.Value).Distinct().ToArray();

        var users = await db.AppUsers
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName, u.Role.RoleName })
            .ToListAsync(ct);

        return users
            .Select(u =>
            {
                var mine = rows.Where(r => r.AssigneeId == u.UserId).ToList();

                return new AssigneeSummaryRow(
                    u.UserId, u.FullName, u.RoleName,
                    mine.Count,
                    mine.Sum(r => r.TargetQty),
                    mine.Sum(r => r.Done),
                    mine.Count(r => r.Deadline != null && r.Deadline < now));
            })
            .OrderBy(r => r.FullName)
            .ToList();
    }

    public Task<List<TaskScript>> GetScriptItemsAsync(long taskId, CancellationToken ct = default) =>
        db.TaskScripts
            .AsNoTracking()
            .Include(ts => ts.Script)
            .Where(ts => ts.TaskId == taskId)
            .OrderBy(ts => ts.IncludedAt).ThenBy(ts => ts.ScriptId)
            .ToListAsync(ct);

    public Task<List<TaskRecording>> GetRecordingItemsAsync(long taskId, CancellationToken ct = default) =>
        db.TaskRecordings
            .AsNoTracking()
            .Include(tr => tr.Recording)
            .Where(tr => tr.TaskId == taskId)
            .OrderBy(tr => tr.IncludedAt).ThenBy(tr => tr.RecordingId)
            .ToListAsync(ct);

    public Task<TaskScript?> GetScriptItemAsync(long taskId, string scriptId, CancellationToken ct = default) =>
        db.TaskScripts.FirstOrDefaultAsync(ts => ts.TaskId == taskId && ts.ScriptId == scriptId, ct);

    public Task<TaskRecording?> GetRecordingItemAsync(
        long taskId, string recordingId, CancellationToken ct = default) =>
        db.TaskRecordings.FirstOrDefaultAsync(tr => tr.TaskId == taskId && tr.RecordingId == recordingId, ct);

    public Task<List<TaskRecording>> GetQueuedRecordingItemsAsync(
        string recordingId, CancellationToken ct = default) =>
        db.TaskRecordings
            .Where(tr => tr.RecordingId == recordingId && tr.Status == TaskRecordingStatus.Queued)
            .ToListAsync(ct);

    public Task<List<TaskScript>> GetPendingScriptItemsAsync(
        string scriptId, CancellationToken ct = default) =>
        db.TaskScripts
            .Where(ts => ts.ScriptId == scriptId && ts.Status == TaskScriptStatus.Pending)
            .ToListAsync(ct);

    public Task<AppUser?> GetUserWithRoleAsync(long userId, CancellationToken ct = default) =>
        db.AppUsers.AsNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public Task<bool> IsActiveTaskOfAsync(
        long taskId, long userId, TaskType type, CancellationToken ct = default) =>
        db.WorkTasks.AsNoTracking().AnyAsync(t =>
            t.TaskId == taskId &&
            t.TaskType == type &&
            (t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.InProgress) &&
            t.Assignments.Any(a => a.UserId == userId && a.AssignmentStatus == AssignmentStatus.Active), ct);

    public Task<List<Script>> GetScriptsAsync(string[] ids, CancellationToken ct = default) =>
        db.Scripts.AsNoTracking()
            .Include(s => s.Recordings)
            .Where(s => ids.Contains(s.ScriptId))
            .ToListAsync(ct);

    public Task<List<Recording>> GetRecordingsWithReviewsAsync(string[] ids, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking()
            .Include(r => r.Reviews)
            .Where(r => ids.Contains(r.RecordingId))
            .ToListAsync(ct);

    public Task<List<string>> FindScriptCandidatesAsync(
        long taskId, long? assigneeId, ScriptDomain? domain, int count, CancellationToken ct = default)
    {
        var query = db.Scripts
            .AsNoTracking()
            .Where(s => s.Status == ScriptStatus.Validated)
            .Where(s => !s.TaskScripts.Any(ts => ts.TaskId == taskId));

        if (domain.HasValue) query = query.Where(s => s.Domain == domain.Value);

        if (assigneeId.HasValue)
        {
            var uid = assigneeId.Value;

            query = query
                // Một cặp câu một người đọc: bỏ qua câu người khác đã đụng vào.
                .Where(s => !s.Recordings.Any(r => r.SpeakerId != uid))
                // Người này còn thiếu ít nhất một biến thể thì mới có việc để làm.
                .Where(s =>
                    !s.Recordings.Any(r => r.SentenceVariant == SentenceVariant.CodeSwitching &&
                        (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved)) ||
                    !s.Recordings.Any(r => r.SentenceVariant == SentenceVariant.PureVietnamese &&
                        (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved)))
                // Chưa nằm trong một task thu âm khác đang giao cho chính người này.
                .Where(s => !s.TaskScripts.Any(ts =>
                    ts.Status == TaskScriptStatus.Pending &&
                    (ts.Task.Status == WorkTaskStatus.Draft ||
                     ts.Task.Status == WorkTaskStatus.Open ||
                     ts.Task.Status == WorkTaskStatus.InProgress) &&
                    ts.Task.Assignments.Any(a => a.UserId == uid && a.AssignmentStatus == AssignmentStatus.Active)));
        }
        else
        {
            // Chưa giao cho ai thì chỉ lấy câu chưa ai thu, để giao cho người nào cũng được.
            query = query.Where(s => !s.Recordings.Any());
        }

        return query
            .OrderBy(s => s.ScriptId)
            .Select(s => s.ScriptId)
            .Take(count)
            .ToListAsync(ct);
    }

    public Task<List<string>> FindRecordingCandidatesAsync(
        long taskId, long? assigneeId, long? speakerId, int count,
        int roundsRequired, CancellationToken ct = default)
    {
        var query = db.Recordings
            .AsNoTracking()
            .Where(r => r.Status == RecordingStatus.PendingReview)
            .Where(r => r.Reviews.Count < roundsRequired)
            .Where(r => !r.TaskRecordings.Any(tr => tr.TaskId == taskId))
            // Chưa bị một task duyệt khác đang chạy giữ.
            .Where(r => !r.TaskRecordings.Any(tr =>
                tr.Status == TaskRecordingStatus.Queued &&
                (tr.Task.Status == WorkTaskStatus.Draft ||
                 tr.Task.Status == WorkTaskStatus.Open ||
                 tr.Task.Status == WorkTaskStatus.InProgress)));

        if (speakerId.HasValue) query = query.Where(r => r.SpeakerId == speakerId.Value);

        if (assigneeId.HasValue)
        {
            var uid = assigneeId.Value;

            query = query
                .Where(r => r.SpeakerId != uid)
                .Where(r => !r.Reviews.Any(v => v.ReviewerId == uid));
        }

        return query
            .OrderBy(r => r.RecordedAt)
            .Select(r => r.RecordingId)
            .Take(count)
            .ToListAsync(ct);
    }

    public Task<List<string>> GetBlockedItemIdsAsync(
        long taskId, long userId, TaskType type, CancellationToken ct = default) =>
        type == TaskType.Recording
            ? db.TaskScripts
                .AsNoTracking()
                .Where(ts => ts.TaskId == taskId && ts.Status == TaskScriptStatus.Pending)
                // Cặp câu đã thuộc về người đọc khác thì người nhận task không đụng vào được.
                .Where(ts => ts.Script.Recordings.Any(r => r.SpeakerId != userId))
                .OrderBy(ts => ts.ScriptId)
                .Select(ts => ts.ScriptId)
                .ToListAsync(ct)
            : db.TaskRecordings
                .AsNoTracking()
                .Where(tr => tr.TaskId == taskId && tr.Status == TaskRecordingStatus.Queued)
                // Bản của chính mình, hoặc mình đã duyệt rồi, thì không duyệt (lại) được.
                .Where(tr => tr.Recording.SpeakerId == userId ||
                             tr.Recording.Reviews.Any(v => v.ReviewerId == userId))
                .OrderBy(tr => tr.RecordingId)
                .Select(tr => tr.RecordingId)
                .ToListAsync(ct);

    public Task<bool> HasSubmittedRecordingAsync(string scriptId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking().AnyAsync(r =>
            r.ScriptId == scriptId &&
            (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved), ct);

    public async Task<bool> IsScriptFullyApprovedAsync(string scriptId, CancellationToken ct = default)
    {
        var approved = await db.Recordings.AsNoTracking()
            .Where(r => r.ScriptId == scriptId && r.Status == RecordingStatus.Approved)
            .Select(r => r.SentenceVariant)
            .Distinct()
            .CountAsync(ct);

        return approved == 2;
    }

    public async Task<(int Submitted, int Approved)> GetSpeakerTotalsAsync(
        long speakerId, CancellationToken ct = default)
    {
        var submitted = await db.Recordings.AsNoTracking()
            .CountAsync(r => r.SpeakerId == speakerId && r.Status != RecordingStatus.QcFailed, ct);

        var approved = await db.Recordings.AsNoTracking()
            .CountAsync(r => r.SpeakerId == speakerId && r.Status == RecordingStatus.Approved, ct);

        return (submitted, approved);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    /// <summary>
    /// Chiếu task sang TaskRow, gồm cả người đang nhận và các con số tiến độ.
    /// Task thu âm đếm theo CẶP CÂU, task duyệt đếm theo bản ghi.
    /// </summary>
    private IQueryable<TaskRow> ProjectRows(IQueryable<WorkTask> source) =>
        source.Select(t => new TaskRow(
            t.TaskId,
            t.TaskType,
            t.Description,
            t.Status,
            t.TargetQty,
            t.Deadline,
            t.CreatedAt,
            t.Assignments
                .Where(a => a.AssignmentStatus == AssignmentStatus.Active)
                .Select(a => (long?)a.UserId)
                .FirstOrDefault(),
            t.Assignments
                .Where(a => a.AssignmentStatus == AssignmentStatus.Active)
                .Select(a => a.User.FullName)
                .FirstOrDefault(),
            t.TaskType == TaskType.Recording
                ? db.Recordings.Count(r => r.TaskId == t.TaskId && r.Status != RecordingStatus.QcFailed)
                : db.Reviews.Count(v => v.TaskId == t.TaskId),
            t.TaskType == TaskType.Recording
                ? t.TaskScripts.Count(ts => ts.Status == TaskScriptStatus.Completed)
                : t.TaskRecordings.Count(tr => tr.Status == TaskRecordingStatus.Reviewed),
            t.TaskType == TaskType.Recording
                ? t.TaskScripts.Count(ts => ts.Status != TaskScriptStatus.Rejected)
                : t.TaskRecordings.Count(tr => tr.Status != TaskRecordingStatus.Skipped),
            t.TaskType == TaskType.Recording
                ? t.TaskScripts.Count
                : t.TaskRecordings.Count));
}
