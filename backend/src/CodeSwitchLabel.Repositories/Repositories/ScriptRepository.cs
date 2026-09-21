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

    Task<Script?> GetAsync(string id, CancellationToken ct = default);
    Task<Script?> GetForUpdateAsync(string id, CancellationToken ct = default);
    Task<Script?> GetWithReviewsAsync(string id, CancellationToken ct = default);

    Task<bool> ContentExistsAsync(string csContent, CancellationToken ct = default);

    /// <summary>
    /// Cặp câu tiếp theo người này nên thu: đã được duyệt nội dung, CHƯA ai khác thu,
    /// và bản thân người này còn thiếu ít nhất một trong hai biến thể cs/vi.
    /// Kèm sẵn các bản ghi của chính người này để tầng trên biết còn thiếu biến thể nào.
    /// </summary>
    Task<Script?> GetNextForSpeakerAsync(long speakerId, long? taskId, CancellationToken ct = default);

    /// <summary>Script đã có bản ghi âm nào chưa — dùng để chặn sửa nội dung sau khi đã thu.</summary>
    Task<bool> HasRecordingsAsync(string scriptId, CancellationToken ct = default);

    /// <summary>Gọi hàm fn_generate_script_id của database để lấy mã đúng chuẩn s_ + 9 chữ số.</summary>
    Task<string> GenerateIdAsync(
        int enWordCount, ScriptDomain domain, ScriptRelation relation, CancellationToken ct = default);

    void Add(Script script);
    void AddReview(ScriptReview review);
    void AddBatch(ImportBatch batch);

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

            // Tìm trên cả câu chen tiếng Anh lẫn câu thuần Việt. ILike dịch thẳng ra
            // toán tử ILIKE của PostgreSQL nên không phải hạ chữ thường cả cột.
            query = query.Where(s =>
                EF.Functions.ILike(s.CsContent, $"%{needle}%") ||
                EF.Functions.ILike(s.VeContent, $"%{needle}%"));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt).ThenBy(s => s.ScriptId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<Script?> GetAsync(string id, CancellationToken ct = default) =>
        db.Scripts.AsNoTracking().FirstOrDefaultAsync(s => s.ScriptId == id, ct);

    public Task<Script?> GetForUpdateAsync(string id, CancellationToken ct = default) =>
        db.Scripts.FirstOrDefaultAsync(s => s.ScriptId == id, ct);

    public Task<Script?> GetWithReviewsAsync(string id, CancellationToken ct = default) =>
        db.Scripts
            .AsNoTracking()
            .Include(s => s.Reviews.OrderByDescending(r => r.ReviewedAt))
                .ThenInclude(r => r.ErrorReason)
            .FirstOrDefaultAsync(s => s.ScriptId == id, ct);

    public Task<bool> ContentExistsAsync(string csContent, CancellationToken ct = default) =>
        db.Scripts.AsNoTracking().AnyAsync(s => s.CsContent == csContent, ct);

    public Task<Script?> GetNextForSpeakerAsync(
        long speakerId, long? taskId, CancellationToken ct = default)
    {
        var query = db.Scripts
            .AsNoTracking()
            .Include(s => s.Recordings.Where(r => r.SpeakerId == speakerId))
            .Where(s => s.Status == ScriptStatus.Validated)

            // Luật của lược đồ: một cặp câu chỉ một người đọc. Câu người khác đã đụng vào
            // thì trigger sẽ chặn, nên đừng phát ra nữa.
            .Where(s => !s.Recordings.Any(r => r.SpeakerId != speakerId))

            // Còn thiếu ít nhất một biến thể. Bản bị từ chối hay trượt QC không tính là đã có.
            .Where(s =>
                !s.Recordings.Any(r => r.SpeakerId == speakerId &&
                    r.SentenceVariant == SentenceVariant.CodeSwitching &&
                    (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved)) ||
                !s.Recordings.Any(r => r.SpeakerId == speakerId &&
                    r.SentenceVariant == SentenceVariant.PureVietnamese &&
                    (r.Status == RecordingStatus.PendingReview || r.Status == RecordingStatus.Approved)));

        if (taskId.HasValue)
        {
            query = query.Where(s => s.TaskScripts.Any(ts =>
                ts.TaskId == taskId.Value && ts.Status == TaskScriptStatus.Pending));
        }

        return query
            // Ưu tiên cặp đang làm dở để người đọc thu nốt bản còn thiếu, rồi mới tới cặp mới.
            .OrderByDescending(s => s.Recordings.Any(r => r.SpeakerId == speakerId))
            .ThenBy(s => s.ScriptId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> HasRecordingsAsync(string scriptId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking().AnyAsync(r => r.ScriptId == scriptId, ct);

    public async Task<string> GenerateIdAsync(
        int enWordCount, ScriptDomain domain, ScriptRelation relation, CancellationToken ct = default)
    {
        // Số thứ tự nằm trong một sequence của database, nên phải để database sinh mã.
        // Ép kiểu tường minh sang script_domain để PostgreSQL chọn đúng hàm.
        var domainName = SnakeCaseNaming.ToSnakeCase(domain.ToString());

        return await db.Database
            .SqlQuery<string>(
                $"""SELECT fn_generate_script_id({enWordCount}, CAST({domainName} AS script_domain), {(int)relation}) AS "Value" """)
            .SingleAsync(ct);
    }

    public void Add(Script script) => db.Scripts.Add(script);
    public void AddReview(ScriptReview review) => db.ScriptReviews.Add(review);
    public void AddBatch(ImportBatch batch) => db.ImportBatches.Add(batch);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
