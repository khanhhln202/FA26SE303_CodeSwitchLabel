using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

public interface IScriptRepository
{
    Task<(IReadOnlyList<Script> Items, int Total)> SearchAsync(
        ScriptStatus? status, ScriptDomain? domain, string? keyword,
        int page, int pageSize, CancellationToken ct = default);

    Task<Script?> GetAsync(long id, CancellationToken ct = default);
    Task<Script?> GetForUpdateAsync(long id, CancellationToken ct = default);
    Task<Script?> GetWithReviewsAsync(long id, CancellationToken ct = default);

    Task<bool> ContentExistsAsync(string content, CancellationToken ct = default);

    /// <summary>
    /// Script tiếp theo Speaker này nên đọc: đã được duyệt, chưa tự tay bỏ qua,
    /// và chưa từng thu. Trả null khi hết — đó là trạng thái bình thường.
    /// </summary>
    Task<Script?> GetNextForSpeakerAsync(long speakerId, long? taskId, CancellationToken ct = default);

    Task<bool> HasSkippedAsync(long scriptId, long speakerId, CancellationToken ct = default);

    /// <summary>
    /// Script đã có bản ghi âm nào chưa. Dùng để chặn sửa nội dung sau khi đã thu —
    /// lược đồ không lưu phiên bản cũ nên sửa là mất khớp transcript vĩnh viễn.
    /// </summary>
    Task<bool> HasRecordingsAsync(long scriptId, CancellationToken ct = default);

    void Add(Script script);
    void AddReview(ScriptReview review);
    void AddSkip(ScriptSkip skip);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class ScriptRepository(CodeSwitchLabelDbContext db) : IScriptRepository
{
    public async Task<(IReadOnlyList<Script> Items, int Total)> SearchAsync(
        ScriptStatus? status, ScriptDomain? domain, string? keyword,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Scripts.AsNoTracking();

        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        if (domain.HasValue) query = query.Where(s => s.Domain == domain.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var needle = keyword.Trim();
            // ILike của Npgsql dịch ra toán tử ILIKE của PostgreSQL — tìm không phân biệt hoa thường
            // mà không phải hạ chữ cả cột, nên index vẫn dùng được.
            query = query.Where(s => EF.Functions.ILike(s.Content, $"%{needle}%"));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.ScriptId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<Script?> GetAsync(long id, CancellationToken ct = default) =>
        db.Scripts.AsNoTracking().FirstOrDefaultAsync(s => s.ScriptId == id, ct);

    public Task<Script?> GetForUpdateAsync(long id, CancellationToken ct = default) =>
        db.Scripts.FirstOrDefaultAsync(s => s.ScriptId == id, ct);

    public Task<Script?> GetWithReviewsAsync(long id, CancellationToken ct = default) =>
        db.Scripts
            .AsNoTracking()
            .Include(s => s.Reviews.OrderByDescending(r => r.ReviewedAt))
                .ThenInclude(r => r.ErrorReason)
            .FirstOrDefaultAsync(s => s.ScriptId == id, ct);

    public Task<bool> ContentExistsAsync(string content, CancellationToken ct = default) =>
        db.Scripts.AsNoTracking().AnyAsync(s => s.Content == content, ct);

    public Task<Script?> GetNextForSpeakerAsync(
        long speakerId, long? taskId, CancellationToken ct = default)
    {
        var query = db.Scripts
            .AsNoTracking()
            .Where(s => s.Status == ScriptStatus.Validated);

        // Có task thì giới hạn trong phạm vi task đó — đúng mô hình giao việc của ERD:
        // Task Manager chốt sẵn danh sách script qua bảng task_script.
        if (taskId.HasValue)
        {
            query = query.Where(s => s.TaskScripts.Any(ts =>
                ts.TaskId == taskId.Value && ts.Status == TaskScriptStatus.Pending));
        }

        return query
            // Người này đã chủ động bỏ qua thì không phát lại.
            .Where(s => !s.Skips.Any(sk => sk.SpeakerId == speakerId))
            // Và đã thu rồi thì cũng không phát lại, trừ khi bản thu bị từ chối.
            .Where(s => !s.Recordings.Any(r =>
                r.SpeakerId == speakerId && r.Status != RecordingStatus.Rejected))
            .OrderBy(s => s.ScriptId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> HasSkippedAsync(long scriptId, long speakerId, CancellationToken ct = default) =>
        db.ScriptSkips.AsNoTracking()
            .AnyAsync(s => s.ScriptId == scriptId && s.SpeakerId == speakerId, ct);

    public Task<bool> HasRecordingsAsync(long scriptId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking().AnyAsync(r => r.ScriptId == scriptId, ct);

    public void Add(Script script) => db.Scripts.Add(script);
    public void AddReview(ScriptReview review) => db.ScriptReviews.Add(review);
    public void AddSkip(ScriptSkip skip) => db.ScriptSkips.Add(skip);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
