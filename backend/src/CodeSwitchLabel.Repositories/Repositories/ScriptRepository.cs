using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

public interface IScriptRepository
{
    /// <summary><paramref name="sort"/> rỗng (Field = null) là thứ tự mặc định.</summary>
    Task<(IReadOnlyList<Script> Items, int Total)> SearchAsync(
        ScriptStatus? status, ScriptDomain? domain, string? keyword,
        int page, int pageSize, SortSpec<ScriptSortField> sort, CancellationToken ct = default);

    Task<Script?> GetAsync(string id, CancellationToken ct = default);
    Task<Script?> GetForUpdateAsync(string id, CancellationToken ct = default);
    Task<Script?> GetWithReviewsAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// câu đã có lượt duyệt nội dung nào chưa. Dùng chặn duyệt lần thứ hai —
    /// mỗi câu chỉ một lượt (yêu cầu 2.3).
    /// </summary>
    Task<bool> HasAcceptedReviewAsync(string scriptId, CancellationToken ct = default);

    /// <summary>
    /// Câu đã có trong kho chưa, so theo khoá chuẩn hoá: bỏ nhãn ngôn ngữ, gộp khoảng
    /// trắng, không phân biệt hoa thường.
    /// </summary>
    Task<bool> ContentExistsAsync(string csContent, CancellationToken ct = default);

    /// <summary>
    /// Lọc các khoá <see cref="CodeSwitchText.ComparisonKey"/> đã có câu tương ứng trong kho.
    /// Trả về chính các khoá đã khớp, không phải nội dung gốc.
    /// </summary>
    Task<HashSet<string>> FindExistingContentsAsync(IEnumerable<string> csContents, CancellationToken ct = default);

    Task<ImportBatch?> GetBatchAsync(long batchId, CancellationToken ct = default);

    Task<(IReadOnlyList<ImportBatch> Items, int Total)> ListBatchesAsync(
        int page, int pageSize, SortSpec<ImportBatchSortField> sort, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetScriptIdsByBatchAsync(long batchId, CancellationToken ct = default);

    /// <summary>Script đã có bản ghi âm nào chưa — dùng để chặn sửa nội dung sau khi đã thu.</summary>
    Task<bool> HasRecordingsAsync(string scriptId, CancellationToken ct = default);

    /// <summary>các bản ghi của CHÍNH người đọc này.</summary>
    Task<List<Recording>> GetRecordingsOfSpeakerAsync(
        string scriptId, long speakerId, CancellationToken ct = default);

    /// <summary>
    /// câu đã có người KHÁC đọc và bản ghi chưa trượt QC. Câu như vậy không
    /// phát ra cho người này được — trigger trg_recording_single_speaker sẽ chặn lúc
    /// thu, tức là sau khi người đó đã đọc xong câu rồi.
    /// </summary>
    Task<bool> IsRecordedByOtherAsync(string scriptId, long speakerId, CancellationToken ct = default);

    /// <summary>Gọi hàm fn_generate_script_id của database để lấy mã đúng chuẩn s_ + 9 hoặc 12 chữ số.</summary>
    Task<string> GenerateIdAsync(
        int enWordCount, ScriptDomain domain, ScriptRelation relation, CancellationToken ct = default);

    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken ct = default);

    void Add(Script script);
    void AddReview(ScriptReview review);
    void AddBatch(ImportBatch batch);
    void RemoveBatch(ImportBatch batch);
    void Remove(Script script);
    void AddWords(IEnumerable<ScriptWord> words);
    void RemoveWords(IEnumerable<ScriptWord> words);

    /// <summary>
    /// Xoá các bảng con của câu không có ON DELETE CASCADE: script_review, script_proposal
    /// và task_script. script_word + script_queue đã cascade ở database.
    /// </summary>
    Task DeleteRelatedAsync(string scriptId, CancellationToken ct = default);

    void ClearTracked();

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class ScriptRepository(CodeSwitchLabelDbContext db) : IScriptRepository
{
    public async Task<(IReadOnlyList<Script> Items, int Total)> SearchAsync(
        ScriptStatus? status, ScriptDomain? domain, string? keyword,
        int page, int pageSize, SortSpec<ScriptSortField> sort, CancellationToken ct = default)
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
                EF.Functions.ILike(s.ViContent, $"%{needle}%"));
        }

        var total = await query.CountAsync(ct);

        // nhánh cuối là thứ tự cũ (mới nhất trước). Mọi nhánh đều nối scriptId
        // ở ngoài switch để trang sau không lặp dòng khi cột sắp xếp bằng nhau.
        var ordered = sort.Field switch
        {
            ScriptSortField.ScriptId => sort.Descending
                ? query.OrderByDescending(s => s.ScriptId)
                : query.OrderBy(s => s.ScriptId),
            ScriptSortField.CreatedAt => sort.Descending
                ? query.OrderByDescending(s => s.CreatedAt)
                : query.OrderBy(s => s.CreatedAt),
            ScriptSortField.Domain => sort.Descending
                ? query.OrderByDescending(s => s.Domain)
                : query.OrderBy(s => s.Domain),
            ScriptSortField.Status => sort.Descending
                ? query.OrderByDescending(s => s.Status)
                : query.OrderBy(s => s.Status),
            _ => query.OrderByDescending(s => s.CreatedAt)
        };

        var items = await ordered
            .ThenBy(s => s.ScriptId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<Script?> GetAsync(string id, CancellationToken ct = default) =>
        db.Scripts.AsNoTracking().FirstOrDefaultAsync(s => s.ScriptId == id, ct);

    public Task<Script?> GetForUpdateAsync(string id, CancellationToken ct = default) =>
        db.Scripts.Include(s => s.Words).FirstOrDefaultAsync(s => s.ScriptId == id, ct);

    public Task<Script?> GetWithReviewsAsync(string id, CancellationToken ct = default) =>
        db.Scripts
            .AsNoTracking()
            .Include(s => s.Words.OrderBy(w => w.WordPosition))
            .Include(s => s.Reviews.OrderByDescending(r => r.ReviewedAt))
                .ThenInclude(r => r.ErrorReason)
            .FirstOrDefaultAsync(s => s.ScriptId == id, ct);

    public Task<bool> HasAcceptedReviewAsync(string scriptId, CancellationToken ct = default) =>
        db.ScriptReviews.AsNoTracking().AnyAsync(
            r => r.ScriptId == scriptId &&
                 (r.Action == ScriptReviewAction.Accepted || r.Action == ScriptReviewAction.Edited), ct);

    public async Task<bool> ContentExistsAsync(string csContent, CancellationToken ct = default) =>
        (await FindExistingContentsAsync([csContent], ct)).Count > 0;

    public async Task<HashSet<string>> FindExistingContentsAsync(
        IEnumerable<string> csContents, CancellationToken ct = default)
    {
        var wanted = csContents
            .Select(CodeSwitchText.ComparisonKey)
            .ToHashSet(StringComparer.Ordinal);

        if (wanted.Count == 0) return [];

        // ponytail: database chỉ có unique index md5(cs_content) khớp CHÍNH XÁC, nên chuẩn
        // hoá phải làm trong bộ nhớ. Khi kho lớn tới mức quét cả bảng thành vấn đề, thêm cột
        // sinh normalized_content + index rồi đẩy phép so xuống SQL; API này giữ nguyên.
        var rows = await db.Scripts.AsNoTracking()
            .Select(s => s.CsContent)
            .ToListAsync(ct);

        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var key = CodeSwitchText.ComparisonKey(row);
            if (wanted.Contains(key)) found.Add(key);
        }

        return found;
    }

    public Task<ImportBatch?> GetBatchAsync(long batchId, CancellationToken ct = default) =>
        db.ImportBatches.AsNoTracking().FirstOrDefaultAsync(b => b.BatchId == batchId, ct);

    public async Task<(IReadOnlyList<ImportBatch> Items, int Total)> ListBatchesAsync(
        int page, int pageSize, SortSpec<ImportBatchSortField> sort, CancellationToken ct = default)
    {
        var query = db.ImportBatches.AsNoTracking();
        var total = await query.CountAsync(ct);

        var ordered = sort.Field switch
        {
            ImportBatchSortField.BatchId => sort.Descending
                ? query.OrderByDescending(b => b.BatchId)
                : query.OrderBy(b => b.BatchId),
            ImportBatchSortField.CreatedAt => sort.Descending
                ? query.OrderByDescending(b => b.CreatedAt)
                : query.OrderBy(b => b.CreatedAt),
            ImportBatchSortField.FileName => sort.Descending
                ? query.OrderByDescending(b => b.FileName)
                : query.OrderBy(b => b.FileName),
            ImportBatchSortField.ScriptCount => sort.Descending
                ? query.OrderByDescending(b => b.ScriptCount)
                : query.OrderBy(b => b.ScriptCount),
            _ => query.OrderByDescending(b => b.BatchId)
        };

        var items = await ordered
            .ThenBy(b => b.BatchId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<string>> GetScriptIdsByBatchAsync(long batchId, CancellationToken ct = default) =>
        await db.Scripts.AsNoTracking()
            .Where(s => s.ImportBatchId == batchId)
            .OrderBy(s => s.ScriptId)
            .Select(s => s.ScriptId)
            .ToListAsync(ct);

    public Task<bool> HasRecordingsAsync(string scriptId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking().AnyAsync(r => r.ScriptId == scriptId, ct);

    public Task<List<Recording>> GetRecordingsOfSpeakerAsync(
        string scriptId, long speakerId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking()
            .Where(r => r.ScriptId == scriptId && r.SpeakerId == speakerId)
            .ToListAsync(ct);

    public Task<bool> IsRecordedByOtherAsync(string scriptId, long speakerId, CancellationToken ct = default) =>
        db.Recordings.AsNoTracking().AnyAsync(
            r => r.ScriptId == scriptId && r.SpeakerId != speakerId && r.Status != RecordingStatus.QcFailed, ct);

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
    public void RemoveBatch(ImportBatch batch) => db.ImportBatches.Remove(batch);
    public void Remove(Script script) => db.Scripts.Remove(script);
    public void AddWords(IEnumerable<ScriptWord> words) => db.ScriptWords.AddRange(words);
    public void RemoveWords(IEnumerable<ScriptWord> words) => db.ScriptWords.RemoveRange(words);
    public void ClearTracked() => db.ChangeTracker.Clear();

    public async Task DeleteRelatedAsync(string scriptId, CancellationToken ct = default)
    {
        await db.ScriptReviews.Where(r => r.ScriptId == scriptId).ExecuteDeleteAsync(ct);
        await db.ScriptProposals.Where(p => p.ScriptId == scriptId).ExecuteDeleteAsync(ct);
        await db.TaskScripts.Where(ts => ts.ScriptId == scriptId).ExecuteDeleteAsync(ct);
    }

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
