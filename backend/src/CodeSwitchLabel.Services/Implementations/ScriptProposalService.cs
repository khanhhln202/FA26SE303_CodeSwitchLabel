using System.Text.Json;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Services.Implementations;

/// <summary>
/// TEAM_015 — nghiệp vụ đề xuất câu.
///
/// Phân chia trách nhiệm với database: các bất biến cứng (câu phải validated và chưa có
/// bản ghi âm; mỗi câu một đề xuất chờ; đề xuất chờ giữ câu khỏi hàng đợi ghi âm) do
/// trigger giữ. Service kiểm tra lại để trả thông báo tiếng Việt rõ ràng, nhưng chốt cuối
/// luôn là lược đồ — đường ghi SQL thẳng cũng không lách được.
///
/// Luật của cặp câu (số từ tiếng Anh, quan hệ Anh–Việt, dựng dòng script_word) nằm ở
/// <see cref="ScriptPairRules"/> — dùng chung với luồng duyệt nội dung, không chép lại.
/// </summary>
public class ScriptProposalService(
    IScriptProposalRepository proposals,
    IScriptRepository repository,
    IReasonRepository reasonRepository,
    IUserDomainRepository domainRepository,
    TimeProvider clock,
    ILogger<ScriptProposalService> logger) : IScriptProposalService
{
    public async Task<ProposalCreatedDto> CreateAsync(
        string scriptId, long proposerId, CreateProposalRequest request, CancellationToken ct = default)
    {
        var script = await repository.GetForUpdateAsync(scriptId, ct)
                     ?? throw NotFoundException.Script(scriptId);

        if (script.Status != ScriptStatus.Validated)
        {
            throw new ConflictException(
                "script_not_validated",
                $"Cặp câu {scriptId} đang ở trạng thái {script.Status}, chỉ gửi đề xuất được " +
                "cho câu đã duyệt nội dung.");
        }

        // Đề xuất chỉ áp dụng cho câu CHƯA thu: sửa nội dung đã có bản ghi âm sẽ làm
        // transcript của bản ghi cũ không còn khớp với âm thanh.
        if (await repository.HasRecordingsAsync(scriptId, ct))
        {
            throw new ConflictException(
                "script_already_recorded",
                $"Cặp câu {scriptId} đã có bản ghi âm nên không gửi đề xuất được. " +
                "Đề xuất chỉ áp dụng cho câu chưa thu.");
        }

        if (await proposals.HasPendingAsync(scriptId, ct))
        {
            throw new ConflictException(
                "proposal_pending_exists",
                $"Cặp câu {scriptId} đang có một đề xuất chờ Reviewer xử lý. " +
                "Chờ đề xuất đó xong rồi gửi tiếp.");
        }

        var kind = request.Kind!.Value;
        var proposal = new ScriptProposal
        {
            ScriptId = scriptId,
            ProposerId = proposerId,
            Kind = kind,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            Status = ProposalStatus.Pending,
            CreatedAt = clock.GetUtcNow()
        };

        if (kind == ProposalKind.Report)
        {
            proposal.ErrorReasonId = await reasonRepository.ResolveActiveReasonIdAsync(
                request.ErrorReasonCode, "Báo lỗi một cặp câu thì bắt buộc chọn lý do.", ct);
        }
        else
        {
            var (cs, ve) = RequireProposedPair(request);
            proposal.ProposedCsContent = cs;
            proposal.ProposedViContent = ve;

            // Kiểm bản sửa NGAY lúc gửi, cùng luật với lượt duyệt sửa: giữ nguyên số từ
            // tiếng Anh và quan hệ Anh–Việt. Đề xuất sai luật không được vào hàng chờ.
            await ScriptPairRules.ValidateEditContentAsync(
                repository, script, cs, ve, request.ProposedAlignment, ct);

            if (request.ProposedAlignment is { Count: > 0 })
            {
                proposal.ProposedAlignment = JsonSerializer.Serialize(request.ProposedAlignment);
            }
        }

        proposals.Add(proposal);

        try
        {
            await proposals.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Hai đề xuất gửi cùng lúc cho một câu: index một phần uq_script_proposal_pending
            // chặn người thứ hai. Kiểm tra ở trên không bắt được ca đua này.
            throw new ConflictException(
                "proposal_pending_exists",
                $"Cặp câu {scriptId} vừa có người khác gửi đề xuất. Chờ đề xuất đó xong rồi gửi tiếp.");
        }

        return new ProposalCreatedDto(
            proposal.ProposalId, proposal.ScriptId, proposal.Kind, proposal.Status, proposal.CreatedAt);
    }

    public async Task<PagedResult<ProposalQueueItemDto>> ListAsync(
        ProposalQueueQuery query, CancellationToken ct = default)
    {
        var (items, total) = await proposals.ListPendingAsync(
            query.Kind, query.Domain, query.Keyword, query.Page, query.PageSize,
            query.ToSort<ProposalSortField>(), ct);

        return new PagedResult<ProposalQueueItemDto>(
            [.. items.Select(ToQueueItem)], query.Page, query.PageSize, total);
    }

    public async Task<ProposalResolutionDto> ResolveAsync(
        long proposalId, long reviewerId, ResolveProposalRequest request, CancellationToken ct = default)
    {
        var decision = request.Decision!.Value;
        var now = clock.GetUtcNow();

        await using var transaction = await BeginTransactionIfNeededAsync(ct);

        // Khoá dòng đề xuất: hai Reviewer bấm cùng lúc thì người thứ hai xếp hàng chờ, đọc
        // thấy quyết định của người thứ nhất rồi mới kiểm tra trạng thái — không ghi đè nhau.
        var proposal = await proposals.LockAsync(proposalId, ct)
                       ?? throw new NotFoundException(
                           "proposal_not_found", $"Không tìm thấy đề xuất #{proposalId}.");

        if (proposal.Status != ProposalStatus.Pending)
        {
            throw new ConflictException(
                "proposal_already_decided",
                $"Đề xuất #{proposalId} đã được xử lý ({proposal.Status}) nên không đổi quyết định được nữa.");
        }

        var script = proposal.Script;
        ScriptReview? review = null;
        long? scriptReviewId = null;

        if (decision == ProposalDecision.Rejected)
        {
            proposal.DecisionNote = RequireDecisionNote(request);
            proposal.Status = ProposalStatus.Rejected;
        }
        else
        {
            // Duyệt = chốt nội dung mới cho câu, nên người duyệt phải đúng chủ đề — cùng
            // luật với duyệt nội dung (trigger trg_script_validated_domain là chốt cuối).
            await domainRepository.EnsureDomainQualifiedAsync(reviewerId, script.Domain, ct);

            var (cs, ve, alignment) = RequireAppliedContent(request, proposal);

            // Cùng luật với lượt duyệt sửa. Ca hiếm: có bản ghi âm xuất hiện trong lúc đề
            // xuất chờ thì duyệt SỬA phải dừng (từ chối thì vẫn xử lý được, xem trên).
            var plan = await ScriptPairRules.ValidateEditContentAsync(
                repository, script, cs, ve, alignment, ct);

            // Lượt duyệt sửa ghi vào lịch sử câu: nội dung đổi lần hai nhờ đề xuất, người
            // chốt là Reviewer. Đây cũng là dấu vết cho màn hình lịch sử duyệt.
            review = new ScriptReview
            {
                ScriptId = script.ScriptId,
                UserId = reviewerId,
                Action = ScriptReviewAction.Edited,
                EditedCsContent = cs,
                EditedViContent = ve,
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                ReviewedAt = now
            };

            repository.AddReview(review);
            ScriptPairRules.ApplyEdit(repository, script, cs, ve, plan);

            proposal.DecisionNote = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            proposal.Status = ProposalStatus.Accepted;
        }

        // Kết quả và mốc quyết định phải nằm trong CÙNG một lần lưu: ràng buộc
        // ck_script_proposal_decision của lược đồ buộc status rời 'pending' đi đôi
        // với decided_at, không cho ghi lệch nhịp.
        proposal.DecidedBy = reviewerId;
        proposal.DecidedAt = now;

        // Trigger fn_script_proposal_sync chạy ở lần lưu này: đề xuất rời trạng thái chờ nên
        // câu tự quay lại hàng đợi ghi âm (nếu vẫn validated và chưa có bản ghi âm nào).
        // Mã lượt duyệt sửa có sau lần lưu này; đọc trước khi commit để còn dùng cho response.
        await proposals.SaveChangesAsync(ct);
        scriptReviewId = review?.ScriptReviewId;

        if (transaction is not null) await transaction.CommitAsync(ct);

        logger.LogInformation(
            "TEAM_015: reviewer {ReviewerId} xử lý đề xuất #{ProposalId} của câu {ScriptId}: {Decision}",
            reviewerId, proposalId, proposal.ScriptId, decision);

        return new ProposalResolutionDto(
            proposal.ProposalId, proposal.ScriptId, proposal.Kind, proposal.Status, script.Status,
            proposal.DecidedBy, proposal.DecidedAt, proposal.DecisionNote, scriptReviewId);
    }

    // ----------------------------------------------------------------- nội bộ

    /// <summary>
    /// Mở transaction riêng, trừ khi caller đã ở trong một transaction (test tích hợp giữ
    /// transaction ambient để rollback). EF không cho transaction lồng nhau trên cùng
    /// connection nên trường hợp đó dùng luôn ambient — cùng cách TaskService.
    /// </summary>
    private async Task<IDbContextTransaction?> BeginTransactionIfNeededAsync(CancellationToken ct)
    {
        try
        {
            return await proposals.BeginTransactionAsync(ct);
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("already in a transaction", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    private static (string Cs, string Ve) RequireProposedPair(CreateProposalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProposedCsContent) ||
            string.IsNullOrWhiteSpace(request.ProposedViContent))
        {
            throw new UnprocessableException(
                "proposed_pair_required",
                "Đề xuất sửa thì phải gửi cả câu chen tiếng Anh lẫn câu thuần Việt đã sửa — " +
                "lược đồ chỉ chấp nhận sửa theo cặp.");
        }

        return (request.ProposedCsContent.Trim(), request.ProposedViContent.Trim());
    }

    private static string RequireDecisionNote(ResolveProposalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            throw new UnprocessableException(
                "decision_note_required",
                "Từ chối đề xuất thì bắt buộc ghi lý do để Speaker biết vì sao.");
        }

        return request.Comment.Trim();
    }

    /// <summary>
    /// Bản chốt lưu vào câu khi duyệt: ưu tiên bản Reviewer gửi lên, không gửi thì lấy bản
    /// sửa của đề xuất Edit. Đề xuất Report không có bản sẵn nên Reviewer bắt buộc phải gửi —
    /// duyệt một báo lỗi mà không sửa gì thì câu vẫn sai như cũ.
    /// </summary>
    private static (string Cs, string Ve, List<AlignmentItem>? Alignment) RequireAppliedContent(
        ResolveProposalRequest request, ScriptProposal proposal)
    {
        var cs = request.AppliedCsContent ?? proposal.ProposedCsContent;
        var ve = request.AppliedViContent ?? proposal.ProposedViContent;

        if (string.IsNullOrWhiteSpace(cs) || string.IsNullOrWhiteSpace(ve))
        {
            throw new UnprocessableException(
                "applied_content_required",
                "Duyệt đề xuất là chốt nội dung mới cho cặp câu, nên phải gửi cả hai câu đã chốt.");
        }

        var alignment = request.AppliedAlignment is { Count: > 0 }
            ? request.AppliedAlignment
            : DeserializeAlignment(proposal.ProposedAlignment);

        return (cs.Trim(), ve.Trim(), alignment);
    }

    private static List<AlignmentItem>? DeserializeAlignment(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<List<AlignmentItem>>(json);

    private static ProposalQueueItemDto ToQueueItem(ScriptProposal p)
    {
        var script = p.Script;

        // Bản đề xuất không kèm lời giải thì Reviewer thấy lời giải hiện có của câu —
        // đúng bản sẽ được giữ nếu duyệt.
        var proposedAlignment = DeserializeAlignment(p.ProposedAlignment)
                                ?? [.. ScriptPairRules.ToAlignment(script.Words)];

        return new ProposalQueueItemDto(
            p.ProposalId, p.Kind, p.Status, p.ScriptId, script.Domain,
            p.ProposerId, p.Proposer.FullName,
            p.ErrorReason?.ReasonCode, p.ErrorReason?.Description,
            p.Comment, p.CreatedAt,
            ToSentence(script.CsContent, script.ViContent, ScriptPairRules.ToAlignment(script.Words)),
            p.ProposedCsContent is null || p.ProposedViContent is null
                ? null
                : ToSentence(p.ProposedCsContent, p.ProposedViContent, proposedAlignment));
    }

    private static ProposalSentenceDto ToSentence(
        string cs, string ve, IReadOnlyList<AlignmentItem> alignment) =>
        new(cs, ScriptPairRules.SafeStrip(cs), ve, ScriptPairRules.SafeStrip(ve), alignment);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException pg &&
        pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation;
}
