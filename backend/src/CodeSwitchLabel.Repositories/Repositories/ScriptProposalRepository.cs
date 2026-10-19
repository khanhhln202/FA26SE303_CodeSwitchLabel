using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Repositories;

/// <summary>
/// TEAM_015 — hàng chờ đề xuất của Reviewer.
///
/// Hàng chờ là truy vấn trên bảng script_proposal (status = 'pending'), KHÔNG phải một
/// chặng của script_queue. Lý do: đề xuất là việc riêng chờ quyết định, không phải bước
/// mà mọi câu đều phải đi qua — ghép vào script_queue sẽ trộn lẫn thứ tự FIFO của chặng
/// ghi âm với thứ tự xử lý đề xuất, hai thứ không liên quan đến nhau.
///
/// Việc "đề xuất chờ thì câu tạm rút khỏi hàng đợi ghi âm" do trigger database lo
/// (fn_script_proposal_sync), không phải repository này — cùng lý do với TEAM_010:
/// đường ghi SQL thẳng cũng phải tuân, không chỉ đường ghi qua service.
/// </summary>
public interface IScriptProposalRepository
{
    void Add(ScriptProposal proposal);

    /// <summary>
    /// Khoá dòng đề xuất (SELECT ... FOR UPDATE) trong transaction hiện tại, kèm câu,
    /// người gửi và lý do lỗi. Dùng cho đường quyết định: hai Reviewer bấm cùng lúc thì
    /// người thứ hai đọc thấy quyết định của người thứ nhất thay vì ghi đè lên.
    /// </summary>
    Task<ScriptProposal?> LockAsync(long proposalId, CancellationToken ct = default);

    /// <summary>Đề xuất đang chờ của một câu — dùng chặn đề xuất thứ hai và chặn hàng ghi âm.</summary>
    Task<bool> HasPendingAsync(string scriptId, CancellationToken ct = default);

    /// <summary>Hàng chờ Reviewer: chỉ đề xuất Pending, mặc định cũ nhất trước (FIFO).</summary>
    Task<(IReadOnlyList<ScriptProposal> Items, int Total)> ListPendingAsync(
        ProposalKind? kind, ScriptDomain? domain, string? keyword,
        int page, int pageSize, SortSpec<ProposalSortField> sort, CancellationToken ct = default);

    /// <summary>
    /// Lịch sử đề xuất của MỘT người gửi — mọi trạng thái, mặc định mới nhất trước.
    /// Ngược chiều <see cref="ListPendingAsync"/>: đó là hàng chờ nên cũ nhất đứng trước,
    /// còn đây là lịch sử nên việc vừa làm đứng trên.
    /// </summary>
    Task<(IReadOnlyList<ScriptProposal> Items, int Total)> ListByProposerAsync(
        long proposerId, ProposalKind? kind, ProposalStatus? status,
        int page, int pageSize, SortSpec<ProposalSortField> sort, CancellationToken ct = default);

    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class ScriptProposalRepository(CodeSwitchLabelDbContext db) : IScriptProposalRepository
{
    public void Add(ScriptProposal proposal) => db.ScriptProposals.Add(proposal);

    public async Task<ScriptProposal?> LockAsync(long proposalId, CancellationToken ct = default)
    {
        // SQL thô vì LINQ không diễn đạt được FOR UPDATE (cùng cách ReviewRepository).
        var rows = await db.ScriptProposals
            .FromSqlInterpolated($"SELECT * FROM script_proposal WHERE proposal_id = {proposalId} FOR UPDATE")
            .Include(p => p.Script).ThenInclude(s => s.Words)
            .Include(p => p.Proposer)
            .Include(p => p.ErrorReason)
            .ToListAsync(ct);

        return rows.FirstOrDefault();
    }

    public Task<bool> HasPendingAsync(string scriptId, CancellationToken ct = default) =>
        db.ScriptProposals.AsNoTracking().AnyAsync(
            p => p.ScriptId == scriptId && p.Status == ProposalStatus.Pending, ct);

    public async Task<(IReadOnlyList<ScriptProposal> Items, int Total)> ListPendingAsync(
        ProposalKind? kind, ScriptDomain? domain, string? keyword,
        int page, int pageSize, SortSpec<ProposalSortField> sort, CancellationToken ct = default)
    {
        var query = db.ScriptProposals.AsNoTracking()
            .Include(p => p.Script).ThenInclude(s => s.Words)
            .Include(p => p.Proposer)
            .Include(p => p.ErrorReason)
            .Where(p => p.Status == ProposalStatus.Pending);

        if (kind.HasValue) query = query.Where(p => p.Kind == kind.Value);
        if (domain.HasValue) query = query.Where(p => p.Script.Domain == domain.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var needle = keyword.Trim();

            // Tìm trên cả hai câu của cặp, bản sửa đề xuất và tên người gửi — đúng những
            // gì Reviewer nhìn thấy trên hàng chờ.
            query = query.Where(p =>
                EF.Functions.ILike(p.Script.CsContent, $"%{needle}%") ||
                EF.Functions.ILike(p.Script.ViContent, $"%{needle}%") ||
                (p.ProposedCsContent != null && EF.Functions.ILike(p.ProposedCsContent, $"%{needle}%")) ||
                (p.ProposedViContent != null && EF.Functions.ILike(p.ProposedViContent, $"%{needle}%")) ||
                EF.Functions.ILike(p.Proposer.FullName, $"%{needle}%"));
        }

        var total = await query.CountAsync(ct);

        // TEAM_012: mọi nhánh đều nối tiếp proposalId để trang sau không lặp hoặc hụt dòng
        // khi cột sắp xếp bằng nhau. Nhánh cuối là thứ tự mặc định — cũ nhất trước, vì đây
        // là hàng chờ: câu chờ lâu nhất phải được xử lý trước.
        var ordered = sort.Field switch
        {
            ProposalSortField.ProposalId => sort.Descending
                ? query.OrderByDescending(p => p.ProposalId)
                : query.OrderBy(p => p.ProposalId),
            ProposalSortField.CreatedAt => sort.Descending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
            ProposalSortField.Kind => sort.Descending
                ? query.OrderByDescending(p => p.Kind)
                : query.OrderBy(p => p.Kind),
            ProposalSortField.ScriptId => sort.Descending
                ? query.OrderByDescending(p => p.ScriptId)
                : query.OrderBy(p => p.ScriptId),
            _ => query.OrderBy(p => p.CreatedAt)
        };

        var items = await ordered
            .ThenBy(p => p.ProposalId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<ScriptProposal> Items, int Total)> ListByProposerAsync(
        long proposerId, ProposalKind? kind, ProposalStatus? status,
        int page, int pageSize, SortSpec<ProposalSortField> sort, CancellationToken ct = default)
    {
        var query = db.ScriptProposals.AsNoTracking()
            .Include(p => p.Script).ThenInclude(s => s.Words)
            .Include(p => p.Proposer)
            .Include(p => p.ErrorReason)
            .Include(p => p.DecidedByUser)
            .Where(p => p.ProposerId == proposerId);

        if (kind.HasValue) query = query.Where(p => p.Kind == kind.Value);
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);

        var total = await query.CountAsync(ct);

        var ordered = sort.Field switch
        {
            ProposalSortField.ProposalId => sort.Descending
                ? query.OrderByDescending(p => p.ProposalId)
                : query.OrderBy(p => p.ProposalId),
            ProposalSortField.CreatedAt => sort.Descending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
            ProposalSortField.Kind => sort.Descending
                ? query.OrderByDescending(p => p.Kind)
                : query.OrderBy(p => p.Kind),
            ProposalSortField.ScriptId => sort.Descending
                ? query.OrderByDescending(p => p.ScriptId)
                : query.OrderBy(p => p.ScriptId),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        // TEAM_012: nối tiếp proposalId để trang sau không lặp hoặc hụt dòng khi cột sắp xếp
        // bằng nhau. Giảm dần ở đây vì nhánh mặc định cũng giảm dần.
        var items = await ordered
            .ThenByDescending(p => p.ProposalId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken ct = default) => db.Database.BeginTransactionAsync(ct);
}
