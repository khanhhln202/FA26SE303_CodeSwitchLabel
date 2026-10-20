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
/// <param name="SubmittedPairs">
/// Task thu âm: số CẶP CÂU đã gửi đủ cả hai bản, chưa cần chờ duyệt. Task duyệt: luôn 0.
/// Đây là con số Speaker nhìn thấy — nộp xong là cộng 1, không bị treo ở việc người duyệt.
/// </param>
/// <param name="UsableItems">Số mục còn làm được — không tính mục đã bị loại.</param>
public record TaskRow(
    long TaskId,
    long? CampaignId,
    string? CampaignName,
    TaskType TaskType,
    ScriptDomain Domain,
    string? Description,
    WorkTaskStatus Status,
    int TargetQty,
    DateTimeOffset? Deadline,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TaskAssigneeRow> Assignees,
    int Started,
    int Done,
    int SubmittedPairs,
    int UsableItems,
    int TotalItems);

/// <summary>TEAM_010: một người đang nhận task. Trước đây TaskRow chỉ giữ một người đầu tiên.</summary>
/// <param name="TargetQty">Quota riêng của người này; null = lượt giao cũ chưa có quota.</param>
public record TaskAssigneeRow(long UserId, string FullName, int? TargetQty = null);

public record AssigneeSummaryRow(
    long UserId, string FullName, RoleName Role, int ActiveTasks, int TotalTarget, int TotalDone, int OverdueTasks);

/// <summary>Một người giao được việc, kèm khối lượng đang gánh để chia việc cho đều.</summary>
public record AssignableUserRow(
    long UserId,
    string FullName,
    RoleName Role,
    int ActiveTasks,
    int TotalTarget,
    decimal? ApprovalRatePct,
    int OverdueTasks,
    string Email = "",
    string? Phone = null);

public interface ITaskRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    void Add(WorkTask task);
    void AddAssignment(TaskAssignment assignment);
    void AddScriptItems(IEnumerable<TaskScript> items);
    void AddRecordingItems(IEnumerable<TaskRecording> items);
    void Remove(TaskScript item);
    void Remove(TaskRecording item);

    /// <summary>
    /// Xoá cứng một task đã nạp sẵn (tracked): task_assignment / task_script /
    /// task_recording đều ON DELETE CASCADE, còn recording.task_id là SET NULL.
    /// Đừng gọi khi task còn dữ liệu nộp — xem <see cref="HasSubmittedWorkAsync"/>.
    /// </summary>
    void RemoveTask(WorkTask task);

    /// <summary>
    /// Task đã có dữ liệu nộp/duyệt THẬT chưa: bản ghi nộp về hoặc lượt duyệt
    /// đang trỏ tới task. Chỉ thứ này mới chặn xoá cứng — lượt giao và hàng chờ
    /// chỉ là sổ sách, xoá task là xoá theo (cascade).
    /// </summary>
    Task<bool> HasSubmittedWorkAsync(long taskId, CancellationToken ct = default);

    Task<WorkTask?> GetForUpdateAsync(long taskId, CancellationToken ct = default);

    /// <summary>
    /// TEAM_010: một task có thể có nhiều người nhận. Hàm cũ trả về đúng một dòng
    /// (FirstOrDefault) nên 8 chỗ gọi nó đều phải sửa — giữ hàm cũ là giữ cái bẫy
    /// "chỉ thấy người đầu tiên".
    /// </summary>
    Task<List<TaskAssignment>> GetActiveAssignmentsAsync(long taskId, CancellationToken ct = default);
    Task<List<TaskAssignment>> GetAssignmentHistoryAsync(long taskId, CancellationToken ct = default);

    Task<TaskRow?> GetRowAsync(long taskId, CancellationToken ct = default);

    /// <summary>TEAM_018: đợt của một task, để định tuyến công việc sang các task cùng đợt.</summary>
    Task<long?> GetCampaignIdAsync(long taskId, CancellationToken ct = default);

    /// <summary>
    /// TEAM_018: các task duyệt còn nhận việc của một đợt (chưa huỷ), theo thứ tự tạo.
    /// Gồm cả task đã đủ chỉ tiêu — TEAM_016: người nhận vẫn duyệt thêm được, KPI không chặn.
    /// </summary>
    Task<List<long>> GetReviewTaskIdsForCampaignAsync(long campaignId, CancellationToken ct = default);

    /// <summary>
    /// Task thu âm duy nhất còn sống của một chiến dịch. Dùng cho luồng
    /// POST /api/campaigns/{id}/recording-task (idempotent).
    /// </summary>
    Task<WorkTask?> GetRecordingTaskForCampaignAsync(long campaignId, CancellationToken ct = default);

    /// <summary>TEAM_012: <paramref name="sort"/> rỗng (Field = null) là thứ tự mặc định.</summary>
    Task<(IReadOnlyList<TaskRow> Items, int Total)> SearchAsync(
        TaskType? type, WorkTaskStatus? status, long? assigneeId, ScriptDomain? domain, bool? overdue,
        DateTimeOffset now, int page, int pageSize, SortSpec<TaskSortField> sort,
        CancellationToken ct = default);

    /// <summary>
    /// Các task còn nhận việc (chưa huỷ) đang giao cho người này. TEAM_016: gồm cả task
    /// đã đủ chỉ tiêu — người nhận vẫn làm thêm được nên màn hình cá nhân phải còn thấy task.
    /// </summary>
    Task<List<TaskRow>> GetActiveRowsForAssigneeAsync(long userId, TaskType type, CancellationToken ct = default);

    /// <summary>
    /// TEAM_010: số mục MỖI NGƯỜI NHẬN đã làm xong, để màn hình cá nhân hiện tiến độ
    /// của chính mình bên cạnh tiến độ chung của cả nhóm.
    ///
    /// Task thu âm đếm cặp câu có bản ghi của đúng người đó; task duyệt đếm lượt
    /// duyệt của đúng người đó. Dùng chung TaskRow.Done cho tất cả sẽ khiến cả ba
    /// người nhận cùng một task thấy một con số giống nhau — sai.
    /// </summary>
    Task<Dictionary<long, int>> GetAssigneeDoneAsync(long taskId, TaskType type, CancellationToken ct = default);

    /// <summary>
    /// TEAM_010: số việc đã NỘP của từng người trong task — khác <see cref="GetAssigneeDoneAsync"/>
    /// ở chỗ nộp rồi vẫn có thể chưa được duyệt đạt.
    /// </summary>
    Task<Dictionary<long, int>> GetAssigneeSubmittedAsync(long taskId, TaskType type, CancellationToken ct = default);
    Task<List<AssigneeSummaryRow>> GetAssigneeSummaryAsync(DateTimeOffset now, CancellationToken ct = default);

    Task<List<TaskScript>> GetScriptItemsAsync(long taskId, CancellationToken ct = default);
    Task<List<TaskRecording>> GetRecordingItemsAsync(long taskId, CancellationToken ct = default);
    Task<TaskScript?> GetScriptItemAsync(long taskId, string scriptId, CancellationToken ct = default);
    Task<TaskRecording?> GetRecordingItemAsync(long taskId, string recordingId, CancellationToken ct = default);
    Task<List<TaskRecording>> GetQueuedRecordingItemsAsync(string recordingId, CancellationToken ct = default);

    /// <summary>
    /// TEAM_018: mọi mục đang chờ của MỌI bản ghi thuộc một cặp câu, ở bất kỳ task nào.
    /// Xoá cả cặp thì tất cả bản ghi của câu bị loại, nên mục chờ ở task duyệt anh em cũng chết theo.
    /// </summary>
    Task<List<TaskRecording>> GetQueuedRecordingItemsByScriptAsync(string scriptId, CancellationToken ct = default);

    /// <summary>Mọi mục còn chờ của một cặp câu, ở bất kỳ task nào. Có theo dõi thay đổi.</summary>
    Task<List<TaskScript>> GetPendingScriptItemsAsync(string scriptId, CancellationToken ct = default);

    /// <summary>
    /// TEAM_010: các mục đã tính là xong của một cặp câu. Bản ghi bị từ chối thì phải
    /// lùi chúng về Pending, nếu không chỉ tiêu không bao giờ tụt lại.
    /// </summary>
    Task<List<TaskScript>> GetCompletedScriptItemsAsync(string scriptId, CancellationToken ct = default);

    /// <summary>TEAM_010: các mục trong task duyệt đã tính là xong cho bản ghi này.</summary>
    Task<List<TaskRecording>> GetReviewedRecordingItemsAsync(string recordingId, CancellationToken ct = default);

    Task<AppUser?> GetUserWithRoleAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// Task đúng loại, còn nhận việc (chưa huỷ), đang giao cho người này. TEAM_016:
    /// task đã đủ chỉ tiêu (Completed) vẫn nhận thêm — KPI không chặn.
    /// </summary>
    Task<bool> IsActiveTaskOfAsync(long taskId, long userId, TaskType type, CancellationToken ct = default);

    Task<HashSet<string>> FindScriptsQueuedInOtherActiveTasksAsync(
        string[] scriptIds, long excludeTaskId, long[] assigneeIds, CancellationToken ct = default);

    /// <summary>
    /// Các mục cùng câu đang nằm trong task khác còn chạy, để chuyển sang task này thay vì báo lỗi.
    /// Cùng điều kiện với <see cref="FindScriptsQueuedInOtherActiveTasksAsync"/>, có theo dõi thay đổi.
    /// </summary>
    Task<List<TaskScript>> GetQueuedScriptItemsInOtherActiveTasksAsync(
        string[] scriptIds, long excludeTaskId, long[] assigneeIds, CancellationToken ct = default);

    Task<HashSet<string>> FindRecordingsQueuedInOtherActiveTasksAsync(
        string[] recordingIds, long excludeTaskId, CancellationToken ct = default);

    /// <summary>Người đang hoạt động có đúng vai, kèm số task và tổng chỉ tiêu đang chạy.</summary>
    Task<List<AssignableUserRow>> GetAssignableUsersAsync(RoleName role, DateTimeOffset now, CancellationToken ct = default);

    Task<List<Script>> GetScriptsAsync(string[] ids, CancellationToken ct = default);
    Task<List<Recording>> GetRecordingsWithReviewsAsync(string[] ids, CancellationToken ct = default);

    Task<List<string>> FindScriptCandidatesAsync(
        long taskId, long[] assigneeIds, ScriptDomain? domain, int count, CancellationToken ct = default);

    Task<List<string>> FindRecordingCandidatesAsync(
        long taskId, long[] assigneeIds, long? speakerId, int count,
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

    public void RemoveTask(WorkTask task) => db.WorkTasks.Remove(task);

    public async Task<bool> HasSubmittedWorkAsync(long taskId, CancellationToken ct = default) =>
        await db.Recordings.AnyAsync(r => r.TaskId == taskId, ct)
            || await db.Reviews.AnyAsync(rv => rv.TaskId == taskId, ct);

    /// <summary>
    /// Khoá dòng task để hai người sửa cùng lúc không ghi đè nhau.
    /// </summary>
    /// <remarks>
    /// Dùng <c>SingleOrDefault</c> chứ không phải <c>FirstOrDefault</c>: câu SQL thô đã lọc theo khoá
    /// chính nên nhiều nhất một dòng. EF không đọc được mệnh đề WHERE nằm trong SQL thô, nên với
    /// <c>First</c> nó tưởng đây là "lấy một dòng bất kỳ trong bảng" và cảnh báo kết quả không đoán được.
    /// </remarks>
    public Task<WorkTask?> GetForUpdateAsync(long taskId, CancellationToken ct = default) =>
        db.WorkTasks.FromSqlInterpolated($"SELECT * FROM task WHERE task_id = {taskId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    /// <summary>TEAM_010: trả về MỌI người đang nhận, theo thứ tự giao. Theo dõi thay đổi để
    /// service đổi trạng thái rồi lưu được trong cùng một lần SaveChanges.</summary>
    public Task<List<TaskAssignment>> GetActiveAssignmentsAsync(long taskId, CancellationToken ct = default) =>
        db.TaskAssignments
            .Where(a => a.TaskId == taskId && a.AssignmentStatus == AssignmentStatus.Active)
            .OrderBy(a => a.AssignedAt)
            .ToListAsync(ct);

    public Task<List<TaskAssignment>> GetAssignmentHistoryAsync(long taskId, CancellationToken ct = default) =>
        db.TaskAssignments
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => a.AssignedAt)
            .ToListAsync(ct);

    public Task<TaskRow?> GetRowAsync(long taskId, CancellationToken ct = default) =>
        ProjectRows(db.WorkTasks.AsNoTracking().Where(t => t.TaskId == taskId)).FirstOrDefaultAsync(ct);

    public Task<long?> GetCampaignIdAsync(long taskId, CancellationToken ct = default) =>
        db.WorkTasks.AsNoTracking()
            .Where(t => t.TaskId == taskId)
            .Select(t => t.CampaignId)
            .FirstOrDefaultAsync(ct);

    public Task<List<long>> GetReviewTaskIdsForCampaignAsync(
        long campaignId, CancellationToken ct = default) =>
        db.WorkTasks.AsNoTracking()
            .Where(t => t.CampaignId == campaignId &&
                        t.TaskType == TaskType.Review &&
                        t.Status != WorkTaskStatus.Cancelled)
            .OrderBy(t => t.TaskId)
            .Select(t => t.TaskId)
            .ToListAsync(ct);

    public Task<WorkTask?> GetRecordingTaskForCampaignAsync(
        long campaignId, CancellationToken ct = default) =>
        db.WorkTasks
            .Where(t => t.CampaignId == campaignId &&
                        t.TaskType == TaskType.Recording &&
                        t.Status != WorkTaskStatus.Cancelled)
            .OrderBy(t => t.TaskId)
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<TaskRow> Items, int Total)> SearchAsync(
        TaskType? type, WorkTaskStatus? status, long? assigneeId, ScriptDomain? domain, bool? overdue,
        DateTimeOffset now, int page, int pageSize, SortSpec<TaskSortField> sort,
        CancellationToken ct = default)
    {
        var query = db.WorkTasks.AsNoTracking();

        if (type.HasValue) query = query.Where(t => t.TaskType == type.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (domain.HasValue) query = query.Where(t => t.Domain == domain.Value);

        if (assigneeId.HasValue)
        {
            query = query.Where(t => t.Assignments.Any(a =>
                a.UserId == assigneeId.Value && a.AssignmentStatus == AssignmentStatus.Active));
        }

        // Quá hạn chỉ có nghĩa với task còn đang làm và có đặt hạn.
        if (overdue == true)
        {
            query = query.Where(t => t.Deadline != null && t.Deadline < now &&
                (t.Status == WorkTaskStatus.InProgress));
        }
        else if (overdue == false)
        {
            query = query.Where(t => !(t.Deadline != null && t.Deadline < now &&
                (t.Status == WorkTaskStatus.InProgress)));
        }

        var total = await query.CountAsync(ct);

        // TEAM_012: nhánh cuối là thứ tự cũ — task có hạn trước, không có hạn ở cuối.
        // Sắp theo deadline cũng giữ nguyên quy tắc đó ở CẢ hai chiều: Postgres mặc định
        // NULLS FIRST khi DESC, nên lật chiều nguyên xi sẽ đẩy task không hạn lên đầu bảng.
        var ordered = sort.Field switch
        {
            TaskSortField.TaskId => sort.Descending
                ? query.OrderByDescending(t => t.TaskId)
                : query.OrderBy(t => t.TaskId),
            TaskSortField.Deadline => sort.Descending
                ? query.OrderBy(t => t.Deadline == null).ThenByDescending(t => t.Deadline)
                : query.OrderBy(t => t.Deadline == null).ThenBy(t => t.Deadline),
            TaskSortField.Status => sort.Descending
                ? query.OrderByDescending(t => t.Status)
                : query.OrderBy(t => t.Status),
            TaskSortField.TaskType => sort.Descending
                ? query.OrderByDescending(t => t.TaskType)
                : query.OrderBy(t => t.TaskType),
            TaskSortField.CreatedAt => sort.Descending
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt),
            TaskSortField.TargetQty => sort.Descending
                ? query.OrderByDescending(t => t.TargetQty)
                : query.OrderBy(t => t.TargetQty),
            _ => query.OrderBy(t => t.Deadline == null).ThenBy(t => t.Deadline)
        };

        // Lọc, sắp xếp, phân trang TRƯỚC khi chiếu sang TaskRow.
        var items = await ProjectRows(ordered
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
                // TEAM_016: trả cả task đã đủ chỉ tiêu — người nhận vẫn làm thêm được.
                .Where(t => t.Status != WorkTaskStatus.Cancelled)
                .Where(t => t.Assignments.Any(a => a.UserId == userId && a.AssignmentStatus == AssignmentStatus.Active))
                .OrderBy(t => t.Deadline))
            .ToListAsync(ct);

    public async Task<Dictionary<long, int>> GetAssigneeSubmittedAsync(
        long taskId, TaskType type, CancellationToken ct = default)
    {
        if (type == TaskType.Recording)
        {
            // Bản trượt QC không tính: người đó vẫn phải thu lại, chưa coi là đã nộp.
            var rows = await db.Recordings.AsNoTracking()
                .Where(r => r.TaskId == taskId && r.Status != RecordingStatus.QcFailed)
                .GroupBy(r => r.SpeakerId)
                .Select(g => new { UserId = g.Key, Submitted = g.Count() })
                .ToListAsync(ct);

            return rows.ToDictionary(x => x.UserId, x => x.Submitted);
        }

        // Task duyệt: "nộp" chính là lượt duyệt đã ghi, nên trùng với Done.
        var reviews = await db.Reviews.AsNoTracking()
            .Where(rv => rv.TaskId == taskId)
            .GroupBy(rv => rv.ReviewerId)
            .Select(g => new { UserId = g.Key, Submitted = g.Count() })
            .ToListAsync(ct);

        return reviews.ToDictionary(x => x.UserId, x => x.Submitted);
    }

    public async Task<Dictionary<long, int>> GetAssigneeDoneAsync(
        long taskId, TaskType type, CancellationToken ct = default)
    {
        var result = new Dictionary<long, int>();

        if (type == TaskType.Recording)
        {
            // Cặp câu tính là xong cho MỘT người khi chính người đó thu đủ bản cs + vi
            // (bản trượt QC không tính — người đó vẫn phải thu lại). Trigger
            // trg_recording_single_speaker bảo đảm một cặp câu chỉ có một người đọc,
            // nên lấy người đọc đầu tiên cũng là người duy nhất.
            var rows = await (from ts in db.TaskScripts.AsNoTracking()
                              where ts.TaskId == taskId && ts.Status == TaskScriptStatus.Completed
                              let speaker = ts.Script.Recordings
                                  .Where(r => r.Status != RecordingStatus.QcFailed)
                                  .Select(r => (long?)r.SpeakerId)
                                  .FirstOrDefault()
                              where speaker != null
                              select new { Speaker = speaker!.Value })
                .ToListAsync(ct);

            foreach (var row in rows) result[row.Speaker] = result.GetValueOrDefault(row.Speaker) + 1;
        }
        else
        {
            // Task duyệt: mỗi người tự đếm số lượt mình đã duyệt, chỉ tính phiếu của người
            // (reviewer_id NULL là lượt của hệ thống nên không thuộc về ai).
            var rows = await db.Reviews
                .AsNoTracking()
                .Where(rv => rv.TaskId == taskId)
                .GroupBy(rv => rv.ReviewerId)
                .Select(g => new { UserId = g.Key, Done = g.Count() })
                .ToListAsync(ct);

            foreach (var row in rows) result[row.UserId] = row.Done;
        }

        return result;
    }

    public async Task<List<AssigneeSummaryRow>> GetAssigneeSummaryAsync(
        DateTimeOffset now, CancellationToken ct = default)
    {
        // Gom trong bộ nhớ: số task đang chạy nhỏ, còn tổng của truy vấn con lồng trong GROUP BY
        // thì EF dịch không ổn định.
        var rows = await ProjectRows(db.WorkTasks
                .AsNoTracking()
                .Where(t => t.Status == WorkTaskStatus.InProgress)
                .Where(t => t.Assignments.Any(a => a.AssignmentStatus == AssignmentStatus.Active)))
            .ToListAsync(ct);

        // TEAM_010: một task thuộc về NHIỀU người, nên mỗi người trong số đó đều phải
        // được tính. Trước đây code lấy AssigneeId của một task rồi gán cho đúng một
        // người — task 3 người thì 2 người biến mất khỏi bảng tải việc, và số liệu
        // "chia đều việc cho từng người" sai hoàn toàn.
        var userIds = rows.SelectMany(r => r.Assignees).Select(a => a.UserId).Distinct().ToArray();

        var users = await db.AppUsers
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName, u.Role.RoleName })
            .ToListAsync(ct);

        return users
            .Select(u =>
            {
                var mine = rows.Where(r => r.Assignees.Any(a => a.UserId == u.UserId)).ToList();

                return new AssigneeSummaryRow(
                    u.UserId, u.FullName, u.RoleName,
                    mine.Count,
                    // Mỗi người chỉ mang quota của mình, không cộng cả chỉ tiêu của nhóm.
                    // Lượt giao cũ chưa có quota thì chia đều để số liệu không nhảy về 0.
                    mine.Sum(r => r.Assignees.First(a => a.UserId == u.UserId).TargetQty
                        ?? DefaultShare(r.TargetQty, r.Assignees.Count)),
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

    public Task<List<TaskRecording>> GetQueuedRecordingItemsByScriptAsync(
        string scriptId, CancellationToken ct = default) =>
        db.TaskRecordings
            .Where(tr => tr.Recording.ScriptId == scriptId && tr.Status == TaskRecordingStatus.Queued)
            .ToListAsync(ct);

    public Task<List<TaskScript>> GetPendingScriptItemsAsync(
        string scriptId, CancellationToken ct = default) =>
        db.TaskScripts
            .Where(ts => ts.ScriptId == scriptId && ts.Status == TaskScriptStatus.Pending)
            .ToListAsync(ct);

    public Task<List<TaskScript>> GetCompletedScriptItemsAsync(
        string scriptId, CancellationToken ct = default) =>
        db.TaskScripts
            .Where(ts => ts.ScriptId == scriptId && ts.Status == TaskScriptStatus.Completed)
            .ToListAsync(ct);

    public Task<List<TaskRecording>> GetReviewedRecordingItemsAsync(
        string recordingId, CancellationToken ct = default) =>
        db.TaskRecordings
            .Where(tr => tr.RecordingId == recordingId && tr.Status == TaskRecordingStatus.Reviewed)
            .ToListAsync(ct);

    public Task<AppUser?> GetUserWithRoleAsync(long userId, CancellationToken ct = default) =>
        db.AppUsers.AsNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public async Task<List<AssignableUserRow>> GetAssignableUsersAsync(RoleName role, DateTimeOffset now, CancellationToken ct = default)
    {
        // TEAM_002: speaker prioritization — reuse v_speaker_performance + assignment load.
        // Sort ranked-first (approval DESC, overdue ASC, load ASC); name last for stability.
        // Speakers with no recordings have NULL rate and sort after rated speakers.
        var users = await db.AppUsers.AsNoTracking()
            .Where(u => u.Status == UserStatus.Active && u.Role.RoleName == role)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                Role = u.Role.RoleName,
                ActiveTasks = db.TaskAssignments.Count(a =>
                    a.UserId == u.UserId &&
                    a.AssignmentStatus == AssignmentStatus.Active &&
                    (a.Task.Status == WorkTaskStatus.InProgress)),
                TotalTarget = db.TaskAssignments
                    .Where(a =>
                        a.UserId == u.UserId &&
                        a.AssignmentStatus == AssignmentStatus.Active &&
                        (a.Task.Status == WorkTaskStatus.InProgress))
                    // Quota riêng đã có từ V12; task cũ chưa backfill thì tạm tính cả task.
                    .Sum(a => (int?)(a.TargetQty ?? a.Task.TargetQty)) ?? 0,
                OverdueTasks = db.TaskAssignments.Count(a =>
                    a.UserId == u.UserId &&
                    a.AssignmentStatus == AssignmentStatus.Active &&
                    (a.Task.Status == WorkTaskStatus.InProgress) &&
                    a.Task.Deadline != null && a.Task.Deadline < now),
                ApprovalRatePct = db.SpeakerPerformance
                    .Where(s => s.UserId == u.UserId)
                    .Select(s => (decimal?)s.ApprovalRatePct)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return [.. users
            .OrderByDescending(u => u.ApprovalRatePct.HasValue)
            .ThenByDescending(u => u.ApprovalRatePct)
            .ThenBy(u => u.OverdueTasks)
            .ThenBy(u => u.ActiveTasks)
            .ThenBy(u => u.FullName)
            .Select(u => new AssignableUserRow(
                u.UserId, u.FullName, u.Role, u.ActiveTasks, u.TotalTarget,
                u.ApprovalRatePct, u.OverdueTasks, u.Email, u.Phone))];
    }

    public Task<bool> IsActiveTaskOfAsync(
        long taskId, long userId, TaskType type, CancellationToken ct = default) =>
        db.WorkTasks.AsNoTracking().AnyAsync(t =>
            t.TaskId == taskId &&
            t.TaskType == type &&
            // TEAM_016: chỉ task đã huỷ mới chặn — task đủ chỉ tiêu vẫn nhận thêm việc.
            t.Status != WorkTaskStatus.Cancelled &&
            t.Assignments.Any(a => a.UserId == userId && a.AssignmentStatus == AssignmentStatus.Active), ct);

    public async Task<HashSet<string>> FindScriptsQueuedInOtherActiveTasksAsync(
        string[] scriptIds, long excludeTaskId, long[] assigneeIds, CancellationToken ct = default)
    {
        if (scriptIds.Length == 0) return [];

        var query = db.TaskScripts.AsNoTracking()
            .Where(ts => scriptIds.Contains(ts.ScriptId) && ts.TaskId != excludeTaskId &&
                         ts.Status == TaskScriptStatus.Pending &&
                         // TEAM_016: task đủ chỉ tiêu vẫn giữ mục chưa xong nên vẫn tính là "đang giữ".
                         ts.Task.Status != WorkTaskStatus.Cancelled);

        // TEAM_010: "cùng người" giờ nghĩa là "có chung ít nhất một người nhận". Task giao
        // cho nhiều người nên câu đang nằm ở task kia thuộc về bất kỳ ai trong số này.
        if (assigneeIds.Length > 0)
        {
            var ids = assigneeIds;
            query = query.Where(ts => ts.Task.Assignments.Any(a =>
                ids.Contains(a.UserId) && a.AssignmentStatus == AssignmentStatus.Active));
        }

        var rows = await query.Select(ts => ts.ScriptId).Distinct().ToListAsync(ct);
        return [.. rows];
    }

    public Task<List<TaskScript>> GetQueuedScriptItemsInOtherActiveTasksAsync(
        string[] scriptIds, long excludeTaskId, long[] assigneeIds, CancellationToken ct = default)
    {
        if (scriptIds.Length == 0) return Task.FromResult(new List<TaskScript>());

        var query = db.TaskScripts
            .Where(ts => scriptIds.Contains(ts.ScriptId) && ts.TaskId != excludeTaskId &&
                         ts.Status == TaskScriptStatus.Pending &&
                         ts.Task.Status != WorkTaskStatus.Cancelled);

        // TEAM_010: "cùng người" giờ nghĩa là "có chung ít nhất một người nhận". Task giao
        // cho nhiều người nên câu đang nằm ở task kia thuộc về bất kỳ ai trong số này.
        if (assigneeIds.Length > 0)
        {
            var ids = assigneeIds;
            query = query.Where(ts => ts.Task.Assignments.Any(a =>
                ids.Contains(a.UserId) && a.AssignmentStatus == AssignmentStatus.Active));
        }

        return query.ToListAsync(ct);
    }

    public async Task<HashSet<string>> FindRecordingsQueuedInOtherActiveTasksAsync(
        string[] recordingIds, long excludeTaskId, CancellationToken ct = default)
    {
        if (recordingIds.Length == 0) return [];

        var rows = await db.TaskRecordings.AsNoTracking()
            .Where(tr => recordingIds.Contains(tr.RecordingId) && tr.TaskId != excludeTaskId &&
                         tr.Status == TaskRecordingStatus.Queued &&
                         // TEAM_016: task đủ chỉ tiêu vẫn giữ mục chưa duyệt nên vẫn tính là "đang giữ".
                         tr.Task.Status != WorkTaskStatus.Cancelled)
            .Select(tr => tr.RecordingId)
            .Distinct()
            .ToListAsync(ct);

        return [.. rows];
    }

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
        long taskId, long[] assigneeIds, ScriptDomain? domain, int count, CancellationToken ct = default)
    {
        var query = db.Scripts
            .AsNoTracking()
            .Where(s => s.Status == ScriptStatus.Validated)
            .Where(s => !s.TaskScripts.Any(ts => ts.TaskId == taskId));

        if (domain.HasValue) query = query.Where(s => s.Domain == domain.Value);

        if (assigneeIds.Length > 0)
        {
            var ids = assigneeIds;

            query = query
                // Một cặp câu một người đọc: bỏ qua câu người khác đã đụng vào.
                // Bản trượt QC không giữ chỗ.
                .Where(s => !s.Recordings.Any(r => !ids.Contains(r.SpeakerId) && r.Status != RecordingStatus.QcFailed))
                // Người này còn thiếu ít nhất một biến thể thì mới có việc để làm.
                .Where(s =>
                    !s.Recordings.Any(r => r.SentenceVariant == SentenceVariant.CodeSwitching &&
                        (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved)) ||
                    !s.Recordings.Any(r => r.SentenceVariant == SentenceVariant.PureVietnamese &&
                        (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved)))
                // Chưa nằm trong task thu âm khác đang giao cho CHÍNH những người này.
                .Where(s => !s.TaskScripts.Any(ts =>
                    ts.Status == TaskScriptStatus.Pending &&
                    // TEAM_016: task đủ chỉ tiêu vẫn giữ mục chưa xong nên vẫn tính là "đang giữ".
                    ts.Task.Status != WorkTaskStatus.Cancelled &&
                    ts.Task.Assignments.Any(a =>
                        ids.Contains(a.UserId) && a.AssignmentStatus == AssignmentStatus.Active)));
        }
        else
        {
            // Chưa giao cho ai thì chỉ lấy câu chưa ai thu (bỏ qua bản trượt QC),
            // để giao cho người nào cũng được.
            query = query.Where(s => !s.Recordings.Any(r => r.Status != RecordingStatus.QcFailed));
        }

        return query
            // FIFO theo hàng đợi chung (yêu cầu 3.1), không phải theo mã câu.
            .OrderBy(s => s.QueueRows
                .Where(q => q.Stage == QueueStage.Recording)
                .OrderBy(q => q.EnqueuedAt)
                .Select(q => (DateTimeOffset?)q.EnqueuedAt)
                .FirstOrDefault())
            .ThenBy(s => s.ScriptId)
            .Select(s => s.ScriptId)
            .Take(count)
            .ToListAsync(ct);
    }

    public Task<List<string>> FindRecordingCandidatesAsync(
        long taskId, long[] assigneeIds, long? speakerId, int count,
        int roundsRequired, CancellationToken ct = default)
    {
        var query = db.Recordings
            .AsNoTracking()
            .Where(r => r.Status == RecordingStatus.PendingReview)
            .Where(r => r.Reviews.Count < roundsRequired)
            .Where(r => !r.TaskRecordings.Any(tr => tr.TaskId == taskId))
            // Chưa bị một task duyệt khác còn nhận việc giữ (TEAM_016: gồm task đủ chỉ tiêu).
            .Where(r => !r.TaskRecordings.Any(tr =>
                tr.Status == TaskRecordingStatus.Queued &&
                tr.Task.Status != WorkTaskStatus.Cancelled));

        if (speakerId.HasValue) query = query.Where(r => r.SpeakerId == speakerId.Value);

        if (assigneeIds.Length > 0)
        {
            var ids = assigneeIds;

            // Bản ghi phải do người NGOÀI nhóm nhận task mà thu, và chưa có ai trong
            // nhóm nào duyệt rồi — nếu không sẽ vi phạm luật duyệt mù 3 phiếu.
            query = query
                .Where(r => !ids.Contains(r.SpeakerId))
                .Where(r => !r.Reviews.Any(v => ids.Contains(v.ReviewerId)));
        }

        return query
            .OrderBy(r => r.RecordedAt)
            .Select(r => r.RecordingId)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetBlockedItemIdsAsync(
        long taskId, long userId, TaskType type, CancellationToken ct = default)
    {
        if (type != TaskType.Recording)
        {
            return await db.TaskRecordings
                .AsNoTracking()
                .Where(tr => tr.TaskId == taskId && tr.Status == TaskRecordingStatus.Queued)
                // Bản của chính mình, hoặc mình đã duyệt rồi, thì không duyệt (lại) được.
                .Where(tr => tr.Recording.SpeakerId == userId ||
                             tr.Recording.Reviews.Any(v => v.ReviewerId == userId))
                .OrderBy(tr => tr.RecordingId)
                .Select(tr => tr.RecordingId)
                .ToListAsync(ct);
        }

        // Cặp câu đã thuộc về người đọc khác thì người nhận task không đụng vào được.
        var ownedByOther = await db.TaskScripts
            .AsNoTracking()
            .Where(ts => ts.TaskId == taskId && ts.Status == TaskScriptStatus.Pending)
            .Where(ts => ts.Script.Recordings.Any(r => r.SpeakerId != userId && r.Status != RecordingStatus.QcFailed))
            .Select(ts => ts.ScriptId)
            .ToListAsync(ct);

        // Người này đã nộp đủ cả hai biến thể (chờ duyệt/đạt) thì không còn việc để làm.
        var fullyDone = await db.TaskScripts
            .AsNoTracking()
            .Where(ts => ts.TaskId == taskId && ts.Status == TaskScriptStatus.Pending)
            .Where(ts => ts.Script.Recordings.Any(r => r.SpeakerId == userId &&
                         r.SentenceVariant == SentenceVariant.CodeSwitching &&
                         (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved)) &&
                         ts.Script.Recordings.Any(r => r.SpeakerId == userId &&
                         r.SentenceVariant == SentenceVariant.PureVietnamese &&
                         (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved)))
            .Select(ts => ts.ScriptId)
            .ToListAsync(ct);

        return [.. ownedByOther.Union(fullyDone).OrderBy(id => id)];
    }

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

    /// <summary>Quota hiển thị cho lượt giao cũ chưa có target_qty (dữ liệu trước V12).</summary>
    private static int DefaultShare(int targetQty, int assigneeCount) =>
        assigneeCount <= 1 ? targetQty : Math.Max(1, (targetQty + assigneeCount - 1) / assigneeCount);

    /// <summary>
    /// Chiếu task sang TaskRow, gồm cả người đang nhận và các con số tiến độ.
    /// Task thu âm đếm theo CẶP CÂU, task duyệt đếm theo bản ghi.
    /// </summary>
    private IQueryable<TaskRow> ProjectRows(IQueryable<WorkTask> source) =>
        source.Select(t => new TaskRow(
            t.TaskId,
            t.CampaignId,
            t.Campaign!.CampaignName,
            t.TaskType,
            t.Domain,
            t.Description,
            t.Status,
            t.TargetQty,
            t.Deadline,
            t.CreatedAt,
            // TEAM_010: lấy HẾT người nhận, không lấy .FirstOrDefault(). Trước đây khi task
            // có nhiều người thì DTO chỉ trả về một người tuỳ ý, còn lại biến mất khỏi
            // danh sách — người quản lý không thấy ai đang làm.
            t.Assignments
                .Where(a => a.AssignmentStatus == AssignmentStatus.Active)
                .OrderBy(a => a.User.FullName)
                .Select(a => new TaskAssigneeRow(a.UserId, a.User.FullName, a.TargetQty))
                .ToList(),
            t.TaskType == TaskType.Recording
                ? db.Recordings.Count(r => r.TaskId == t.TaskId && r.Status != RecordingStatus.QcFailed)
                : db.Reviews.Count(v => v.TaskId == t.TaskId),
            t.TaskType == TaskType.Recording
                ? t.TaskScripts.Count(ts => ts.Status == TaskScriptStatus.Completed)
                : t.TaskRecordings.Count(tr => tr.Status == TaskRecordingStatus.Reviewed),
            t.TaskType == TaskType.Recording
                ? t.TaskScripts.Count(ts =>
                    ts.Status != TaskScriptStatus.Rejected &&
                    ts.Script.Recordings.Any(r =>
                        r.SentenceVariant == SentenceVariant.CodeSwitching && r.Status != RecordingStatus.QcFailed) &&
                    ts.Script.Recordings.Any(r =>
                        r.SentenceVariant == SentenceVariant.PureVietnamese && r.Status != RecordingStatus.QcFailed))
                : 0,
            t.TaskType == TaskType.Recording
                ? t.TaskScripts.Count(ts => ts.Status != TaskScriptStatus.Rejected)
                : t.TaskRecordings.Count(tr => tr.Status != TaskRecordingStatus.Skipped),
            t.TaskType == TaskType.Recording
                ? t.TaskScripts.Count
                : t.TaskRecordings.Count));
}
