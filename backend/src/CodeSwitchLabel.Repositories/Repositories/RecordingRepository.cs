using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeSwitchLabel.Repositories.Repositories;

public interface IRecordingRepository
{
    /// <summary>
    /// chuyển mọi bản ghi của cặp câu sang rejected — dùng cho nút
    /// "Xóa hoàn toàn". Trả về số bản bị đổi.
    /// </summary>
    Task<int> MarkAllRejectedAsync(string scriptId, CancellationToken ct = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    Task<Recording?> GetAsync(string recordingId, CancellationToken ct = default);

    /// <summary>
    /// Lần thu đang hiệu lực của một biến thể — bản chờ duyệt hoặc đã đạt. Null nghĩa là
    /// biến thể đó đang trống (chưa thu, hoặc bản cũ đã bị loại / trượt QC).
    /// Trả về thực thể CÓ theo dõi thay đổi: người gọi sửa trạng thái rồi lưu.
    /// </summary>
    Task<Recording?> GetActiveTakeAsync(
        string scriptId, SentenceVariant variant, CancellationToken ct = default);

    /// <summary><paramref name="sort"/> rỗng (Field = null) là thứ tự mặc định.</summary>
    Task<(IReadOnlyList<Recording> Items, int Total)> SearchAsync(
        long? speakerId, string? scriptId, RecordingStatus? status,
        int page, int pageSize, SortSpec<RecordingSortField> sort, CancellationToken ct = default);

    /// <summary>
    /// Người đang giữ cặp câu này (có bản chờ duyệt/đạt). Bản rejected/qc_failed không
    /// giữ quyền — sau khi Reviewer loại, cặp câu được thu lại bởi người khác.
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

    /// <summary>
    /// Task thu âm chứa cặp câu, trong phạm vi chiến dịch của task được hỏi, mà người này
    /// đang nhận việc. Trả task sẽ ghi vào bản ghi (ưu tiên đúng task được hỏi), null nếu
    /// không thu được. Task đã đủ chỉ tiêu (Completed) vẫn nhận — KPI không chặn.
    /// </summary>
    Task<long?> FindRecordableTaskIdAsync(
        long requestedTaskId, long speakerId, string scriptId, CancellationToken ct = default);

    /// <summary>
    /// Task thu âm còn nhận việc (chưa huỷ), đang giao cho người này, mà còn chứa cặp câu
    /// này ở phần chưa xong. Dùng để tự gắn bản ghi vào task khi người gọi không gửi taskId.
    /// </summary>
    Task<long?> FindOwnTaskContainingScriptAsync(
        long speakerId, string scriptId, CancellationToken ct = default);

    void Add(Recording recording);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class RecordingRepository(CodeSwitchLabelDbContext db) : IRecordingRepository
{
    /// <summary>
    /// ExecuteUpdate nên không nạp hàng: không cần đọc file audio hay review của bản
    /// ghi, chỉ cần đổi cột trạng thái.
    /// </summary>
    public Task<int> MarkAllRejectedAsync(string scriptId, CancellationToken ct = default) =>
        db.Recordings
            .Where(r => r.ScriptId == scriptId && r.Status != RecordingStatus.Rejected)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RecordingStatus.Rejected), ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);

    public Task<Recording?> GetActiveTakeAsync(
        string scriptId, SentenceVariant variant, CancellationToken ct = default) =>
        db.Recordings.FirstOrDefaultAsync(
            r => r.ScriptId == scriptId &&
                 r.SentenceVariant == variant &&
                 (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved),
            ct);

    public Task<Recording?> GetAsync(string recordingId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking().FirstOrDefaultAsync(r => r.RecordingId == recordingId, ct);

    public async Task<(IReadOnlyList<Recording> Items, int Total)> SearchAsync(
        long? speakerId, string? scriptId, RecordingStatus? status,
        int page, int pageSize, SortSpec<RecordingSortField> sort, CancellationToken ct = default)
    {
        var query = db.Recordings.AsNoTracking();

        if (speakerId.HasValue) query = query.Where(r => r.SpeakerId == speakerId.Value);
        if (!string.IsNullOrWhiteSpace(scriptId)) query = query.Where(r => r.ScriptId == scriptId);
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);

        var total = await query.CountAsync(ct);

        // nhánh cuối là thứ tự cũ (bản ghi mới nhất trước).
        var ordered = sort.Field switch
        {
            RecordingSortField.RecordingId => sort.Descending
                ? query.OrderByDescending(r => r.RecordingId)
                : query.OrderBy(r => r.RecordingId),
            RecordingSortField.RecordedAt => sort.Descending
                ? query.OrderByDescending(r => r.RecordedAt)
                : query.OrderBy(r => r.RecordedAt),
            RecordingSortField.Status => sort.Descending
                ? query.OrderByDescending(r => r.Status)
                : query.OrderBy(r => r.Status),
            RecordingSortField.ScriptId => sort.Descending
                ? query.OrderByDescending(r => r.ScriptId)
                : query.OrderBy(r => r.ScriptId),
            RecordingSortField.DurationSec => sort.Descending
                ? query.OrderByDescending(r => r.DurationSec)
                : query.OrderBy(r => r.DurationSec),
            _ => query.OrderByDescending(r => r.RecordedAt)
        };

        var items = await ordered
            .ThenBy(r => r.RecordingId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<long?> GetOwnerSpeakerIdAsync(string scriptId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking()
            .Where(r => r.ScriptId == scriptId &&
                        r.Status != RecordingStatus.QcFailed &&
                        r.Status != RecordingStatus.Rejected)
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

    public async Task<long?> FindRecordableTaskIdAsync(
        long requestedTaskId, long speakerId, string scriptId, CancellationToken ct = default)
    {
        var scope = await db.WorkTasks.AsNoTracking()
            .Where(t => t.TaskId == requestedTaskId)
            .Select(t => new { t.TaskId, t.TaskType, t.Status, t.CampaignId })
            .FirstOrDefaultAsync(ct);

        if (scope is null ||
            scope.TaskType != TaskType.Recording ||
            // chỉ task đã huỷ mới chặn — task đủ chỉ tiêu vẫn thu thêm được.
            scope.Status == WorkTaskStatus.Cancelled)
        {
            return null;
        }

        // Nhận việc ở BẤT KỲ task thu âm nào của cùng chiến dịch, không chỉ task được hỏi.
        var assigned = await db.TaskAssignments.AsNoTracking().AnyAsync(a =>
            a.UserId == speakerId &&
            a.AssignmentStatus != AssignmentStatus.Reassigned &&
            a.AssignmentStatus != AssignmentStatus.Cancelled &&
            (a.TaskId == scope.TaskId ||
             (scope.CampaignId != null &&
              a.Task.CampaignId == scope.CampaignId &&
              a.Task.TaskType == TaskType.Recording &&
              a.Task.Status != WorkTaskStatus.Cancelled)), ct);

        if (!assigned) return null;

        return await db.TaskScripts.AsNoTracking()
            .Where(ts => ts.ScriptId == scriptId && ts.Status == TaskScriptStatus.Pending)
            .Where(ts => ts.Task.TaskType == TaskType.Recording &&
                         ts.Task.Status != WorkTaskStatus.Cancelled)
            .Where(ts => ts.TaskId == scope.TaskId ||
                         (scope.CampaignId != null && ts.Task.CampaignId == scope.CampaignId))
            // Ưu tiên task được hỏi để bản ghi vào đúng chỗ người dùng đang làm.
            .OrderByDescending(ts => ts.TaskId == scope.TaskId)
            .Select(ts => (long?)ts.TaskId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<long?> FindOwnTaskContainingScriptAsync(
        long speakerId, string scriptId, CancellationToken ct = default) =>
        db.WorkTasks
            .AsNoTracking()
            .Where(t => t.TaskType == TaskType.Recording)
            .Where(t => t.Status != WorkTaskStatus.Cancelled)
            .Where(t => t.Assignments.Any(a => a.UserId == speakerId && a.AssignmentStatus != AssignmentStatus.Reassigned && a.AssignmentStatus != AssignmentStatus.Cancelled))
            .Where(t => t.TaskScripts.Any(ts => ts.ScriptId == scriptId && ts.Status == TaskScriptStatus.Pending))
            .OrderBy(t => t.Deadline ?? DateTimeOffset.MaxValue)
            .Select(t => (long?)t.TaskId)
            .FirstOrDefaultAsync(ct);

    public void Add(Recording recording) => db.Recordings.Add(recording);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
