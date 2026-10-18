using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

/// <summary>
/// TEAM_010 — hàng đợi chung cho duyệt văn bản và ghi âm.
///
/// Mọi thao tác giữ chỗ đi qua hàm của database chứ không phải truy vấn EF.
/// Lý do: "không cho hai người cùng nhận một câu" (yêu cầu 3.2) chỉ đúng khi việc
/// đọc hàng và ghi chủ xảy ra trong CÙNG một transaction, với khoá tương tác. Nếu
/// làm bằng EF thì khoảng giữa SELECT và UPDATE là lúc trống để người khác chen vào.
/// SKIP LOCKED trong hàm database loại được đúng khoảng trống đó.
/// </summary>
public interface IScriptQueueRepository
{
    /// <summary>
    /// Lấy câu kế tiếp trong hàng đợi theo thứ tự FIFO và giữ lại cho người này.
    /// Trả null khi hàng đợi rỗng — kể cả khi mọi câu đang bị giữ và chưa hết hạn.
    /// </summary>
    Task<QueueClaimDto?> ClaimNextAsync(
        long userId, QueueStage stage, long? taskId = null, CancellationToken ct = default);

    /// <summary>
    /// Chia lô câu cho một task duyệt văn bản (yêu cầu 2.2). Trả về các mã câu thực sự
    /// lấy được — có thể ít hơn <paramref name="count"/> khi hàng đợi không đủ câu.
    /// </summary>
    Task<IReadOnlyList<string>> ClaimBatchAsync(
        long taskId, long userId, int count, QueueStage stage = QueueStage.TextReview,
        CancellationToken ct = default);

    /// <summary>
    /// Đóng claim của một câu. <paramref name="deferToBack"/> đẩy câu xuống CUỐI hàng
    /// đợi thay vì giữ nguyên chỗ cũ — bắt buộc cho trường hợp "lấy được nhưng không
    /// dùng được", nếu không câu đó vẫn đứng đầu và bị claim lại ngay lập tức.
    /// </summary>
    Task<bool> ReleaseAsync(
        string scriptId, QueueStage stage, QueueState state = QueueState.Queued,
        bool deferToBack = false, CancellationToken ct = default);

    /// <summary>Đưa câu trở lại hàng đợi, kể cả khi hàng của nó đang ở trạng thái <c>done</c>.</summary>
    Task RequeueAsync(string scriptId, QueueStage stage, CancellationToken ct = default);

    /// <summary>Số câu đang chờ ai đó lấy, và số câu đang bị giữ. Cho màn hình Task Manager.</summary>
    Task<QueueStatsDto> GetStatsAsync(QueueStage stage, CancellationToken ct = default);

    /// <summary>
    /// Hàng đang mở của câu, gồm cả lô task đã gán. Cần để kiểm tra "người này có
    /// được duyệt câu này không" mà không phải dò thêm một vòng.
    /// </summary>
    Task<ScriptQueue?> GetOpenRowAsync(string scriptId, QueueStage stage, CancellationToken ct = default);

    /// <summary>Câu đang chờ duyệt nội dung, cũ nhất trước, kèm người đóng góp.</summary>
    Task<(IReadOnlyList<PendingContributionRow> Items, int Total)> ListPendingContributionsAsync(
        ScriptDomain? domain, int page, int pageSize, CancellationToken ct = default);
}

public record QueueClaimDto(string ScriptId, long QueueId, DateTimeOffset EnqueuedAt, DateTimeOffset ClaimExpiresAt);

public record QueueStatsDto(int Queued, int Claimed);

/// <summary>
/// Một câu đang chờ duyệt nội dung, kèm người đóng góp và lô đang giữ nó (nếu có).
/// <paramref name="TaskId"/> null nghĩa là câu còn trong hàng đợi chung, ai cũng duyệt được.
/// </summary>
public record PendingContributionRow(
    string ScriptId, string CsContent, string ViContent, ScriptDomain Domain,
    DateTimeOffset CreatedAt, long ContributorId, string ContributorName, long? TaskId);

public class ScriptQueueRepository(CodeSwitchLabelDbContext db) : IScriptQueueRepository
{
    public async Task<QueueClaimDto?> ClaimNextAsync(
        long userId, QueueStage stage, long? taskId = null, CancellationToken ct = default)
    {
        // Ép kiểu tường minh sang queue_stage để PostgreSQL chọn đúng hàm khi có
        // tham số NULL (task_id) — nếu không sẽ không suy ra được kiểu.
        var stageName = SnakeCaseNaming.ToSnakeCase(stage.ToString());

        var rows = await db.Database
            .SqlQuery<ClaimRow>(
                $"""SELECT script_id AS "ScriptId", queue_id AS "QueueId", enqueued_at AS "EnqueuedAt", claim_expires_at AS "ClaimExpiresAt" FROM fn_queue_claim_next({userId}, CAST({stageName} AS queue_stage), {(long?)taskId})""")
            .ToListAsync(ct);

        return rows.Count == 0
            ? null
            : new QueueClaimDto(rows[0].ScriptId, rows[0].QueueId, rows[0].EnqueuedAt, rows[0].ClaimExpiresAt);
    }

    public async Task<IReadOnlyList<string>> ClaimBatchAsync(
        long taskId, long userId, int count, QueueStage stage = QueueStage.TextReview,
        CancellationToken ct = default)
    {
        if (count <= 0) return [];

        var stageName = SnakeCaseNaming.ToSnakeCase(stage.ToString());

        return await db.Database
            .SqlQuery<string>(
                $"""SELECT * FROM fn_queue_claim_batch({taskId}, {userId}, {count}, CAST({stageName} AS queue_stage))""")
            .ToListAsync(ct);
    }

    public async Task<bool> ReleaseAsync(
        string scriptId, QueueStage stage, QueueState state = QueueState.Queued,
        bool deferToBack = false, CancellationToken ct = default)
    {
        var stageName = SnakeCaseNaming.ToSnakeCase(stage.ToString());
        var stateName = SnakeCaseNaming.ToSnakeCase(state.ToString());

        var affected = await db.Database
            .SqlQuery<int>(
                $"""SELECT fn_queue_release({scriptId}, CAST({stageName} AS queue_stage), CAST({stateName} AS queue_state), 'claimed'::queue_state, {deferToBack}) AS "Value" """)
            .SingleAsync(ct);

        return affected > 0;
    }

    public async Task RequeueAsync(string scriptId, QueueStage stage, CancellationToken ct = default)
    {
        var stageName = SnakeCaseNaming.ToSnakeCase(stage.ToString());

        await db.Database
            .SqlQuery<int>(
                $"""SELECT fn_queue_requeue({scriptId}, CAST({stageName} AS queue_stage)) AS "Value" """)
            .SingleAsync(ct);
    }

    public async Task<QueueStatsDto> GetStatsAsync(QueueStage stage, CancellationToken ct = default)
    {
        var queued = await db.ScriptQueue.AsNoTracking()
            .CountAsync(q => q.Stage == stage && q.State == QueueState.Queued, ct);

        var claimed = await db.ScriptQueue.AsNoTracking()
            .CountAsync(q => q.Stage == stage && q.State == QueueState.Claimed, ct);

        return new QueueStatsDto(queued, claimed);
    }

    public async Task<(IReadOnlyList<PendingContributionRow> Items, int Total)> ListPendingContributionsAsync(
        ScriptDomain? domain, int page, int pageSize, CancellationToken ct = default)
    {
        var scripts = db.Scripts.AsNoTracking()
            .Where(s => s.Status == ScriptStatus.PendingValidation);

        if (domain is not null) scripts = scripts.Where(s => s.Domain == domain);

        var total = await scripts.CountAsync(ct);

        // Cũ nhất trước: câu chờ lâu nhất được duyệt trước, giống thứ tự FIFO của hàng đợi.
        var items = await scripts
            .OrderBy(s => s.CreatedAt).ThenBy(s => s.ScriptId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new PendingContributionRow(
                s.ScriptId, s.CsContent, s.ViContent, s.Domain, s.CreatedAt,
                s.CreatedBy, s.Creator.FullName,
                db.ScriptQueue
                    .Where(q => q.ScriptId == s.ScriptId && q.Stage == QueueStage.TextReview &&
                                (q.State == QueueState.Queued || q.State == QueueState.Claimed))
                    .Select(q => q.TaskId)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<ScriptQueue?> GetOpenRowAsync(string scriptId, QueueStage stage, CancellationToken ct = default) =>
        db.ScriptQueue.AsNoTracking()
            .Where(q => q.ScriptId == scriptId && q.Stage == stage &&
                        (q.State == QueueState.Queued || q.State == QueueState.Claimed))
            .FirstOrDefaultAsync(ct);

    private sealed record ClaimRow(string ScriptId, long QueueId, DateTimeOffset EnqueuedAt, DateTimeOffset ClaimExpiresAt);
}
