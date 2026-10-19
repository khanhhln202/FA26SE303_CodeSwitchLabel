using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Reviews;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Services.Implementations;

public class ReviewerStatsService(
    CodeSwitchLabelDbContext db,
    IObjectStorage storage) : IReviewerStatsService
{
    public async Task<ReviewerTotalsDto> GetTotalsAsync(long reviewerId, CancellationToken ct = default)
    {
        var approved = await db.Reviews.AsNoTracking()
            .CountAsync(v => v.ReviewerId == reviewerId && v.Decision == ReviewDecision.Approved, ct);
        var rejected = await db.Reviews.AsNoTracking()
            .CountAsync(v => v.ReviewerId == reviewerId && v.Decision == ReviewDecision.Rejected, ct);

        return new ReviewerTotalsDto(approved, rejected);
    }

    public async Task<IReadOnlyList<RejectReasonStatDto>> GetRejectReasonsAsync(
        long reviewerId, CancellationToken ct = default)
    {
        var rows = await db.Reviews.AsNoTracking()
            .Where(v => v.ReviewerId == reviewerId && v.Decision == ReviewDecision.Rejected)
            .SelectMany(v => v.RejectionReasons.Select(rr => rr.Reason.Category))
            .GroupBy(c => c)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total = rows.Sum(x => x.Count);
        if (total == 0) return [];

        return [.. rows
            .OrderByDescending(x => x.Count)
            .Select(x => new RejectReasonStatDto(
                CategoryLabel(x.Category),
                Math.Round(100.0 * x.Count / total, 1),
                $"{x.Count} lần"))];
    }

    public async Task<IReadOnlyList<TopRejectedSentenceDto>> GetTopRejectedSentencesAsync(
        long reviewerId, int limit, CancellationToken ct = default)
    {
        // Tải lượt từ chối của mình kèm câu + lý do, gom nhóm trong bộ nhớ để giữ
        // breakdown theo category và ví dụ comment — số lượt của một reviewer là nhỏ.
        var rows = await db.Reviews.AsNoTracking()
            .Include(v => v.RejectionReasons).ThenInclude(rr => rr.Reason)
            .Include(v => v.Recording).ThenInclude(r => r!.Script).ThenInclude(s => s.Words)
            .Include(v => v.Task)
            .Where(v => v.ReviewerId == reviewerId && v.Decision == ReviewDecision.Rejected)
            .ToListAsync(ct);

        return [.. rows
            .GroupBy(v => v.Recording!.ScriptId)
            .Select(g =>
            {
                var script = g.First().Recording!.Script;
                var breakdown = g
                    .SelectMany(v => v.RejectionReasons.Select(rr => rr.Reason.Category))
                    .GroupBy(c => c)
                    .Select(x => new CategoryBreakdownDto(CategoryLabel(x.Key), x.Count()))
                    .OrderByDescending(x => x.Count)
                    .ToList();

                return new TopRejectedSentenceDto(
                    script.ScriptId,
                    CodeSwitchText.Strip(script.CsContent),
                    CodeSwitchText.Strip(script.ViContent),
                    [.. script.Words.OrderBy(w => w.WordPosition).Select(w => w.EnWord).Distinct()],
                    g.First().Task?.Description,
                    g.Count(),
                    breakdown,
                    g.Select(v => v.Comment).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)));
            })
            .OrderByDescending(x => x.Count)
            .Take(Math.Clamp(limit, 1, 20))];
    }

    public async Task<IReadOnlyList<TopRejectedSpeakerDto>> GetTopRejectedSpeakersAsync(
        long reviewerId, int limit, CancellationToken ct = default)
    {
        var rows = await db.Reviews.AsNoTracking()
            .Include(v => v.RejectionReasons).ThenInclude(rr => rr.Reason)
            .Include(v => v.Recording).ThenInclude(r => r!.Speaker)
            .Where(v => v.ReviewerId == reviewerId && v.Decision == ReviewDecision.Rejected)
            .ToListAsync(ct);

        return [.. rows
            .GroupBy(v => v.Recording!.SpeakerId)
            .Select(g =>
            {
                var breakdown = g
                    .SelectMany(v => v.RejectionReasons.Select(rr => rr.Reason.Category))
                    .GroupBy(c => c)
                    .Select(x => new CategoryBreakdownDto(CategoryLabel(x.Key), x.Count()))
                    .OrderByDescending(x => x.Count)
                    .ToList();

                return new TopRejectedSpeakerDto(
                    g.Key,
                    g.First().Recording!.Speaker.FullName,
                    g.Count(),
                    breakdown.FirstOrDefault()?.Category,
                    breakdown,
                    g.Select(v => v.Comment).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)));
            })
            .OrderByDescending(x => x.Count)
            .Take(Math.Clamp(limit, 1, 20))];
    }

    public async Task<PagedResult<ReviewerHistoryItemDto>> GetHistoryAsync(
        long reviewerId, ReviewerHistoryQuery query, CancellationToken ct = default)
    {
        var q = db.Reviews.AsNoTracking()
            .Include(v => v.Recording).ThenInclude(r => r!.Script)
            .Include(v => v.Recording).ThenInclude(r => r!.Speaker)
            .Include(v => v.Task)
            .Where(v => v.ReviewerId == reviewerId);

        if (query.TaskId.HasValue) q = q.Where(v => v.TaskId == query.TaskId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<RecordingStatus>(query.Status.Trim(), true, out var status))
        {
            q = q.Where(v => v.Recording!.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search.Trim();
            q = q.Where(v => v.RecordingId.Contains(needle)
                || v.Recording!.ScriptId.Contains(needle)
                || v.Recording!.Script.CsContent.Contains(needle)
                || v.Recording!.Speaker.FullName.Contains(needle));
        }

        // Một dòng là MỘT CẶP bản ghi, nên phân trang theo cặp chứ không theo lượt duyệt:
        // gom scriptId trước khi cắt trang. (Distinct + OrderBy thì Postgres không nhận,
        // nên gom bằng GroupBy/Max — đúng cách lịch sử ghi âm của Speaker đang làm.)
        //
        // `myDecision` gộp ngay trong truy vấn gom nhóm: có lượt từ chối ở bất kỳ bản nào
        // thì cả cặp là từ chối. Max của cờ 0/1 chính là phép "hoặc" đó, và dịch được xuống SQL.
        var total = await q.Select(v => v.Recording!.ScriptId).Distinct().CountAsync(ct);

        var pairs = q
            .GroupBy(v => v.Recording!.ScriptId)
            .Select(g => new
            {
                ScriptId = g.Key,
                LastReviewedAt = g.Max(v => v.ReviewedAt),
                HasReject = g.Max(v => v.Decision == ReviewDecision.Rejected ? 1 : 0)
            });

        var sort = query.ToSort<ReviewSortField>();

        // TEAM_012: chỉ ba cột của cặp là sắp xếp được TRƯỚC khi cắt trang. Trạng thái của
        // cặp gộp từ hai bản nên phải TÍNH RA sau đó — cùng lý do với SpeakerPairSortField.
        var ordered = sort.Field switch
        {
            ReviewSortField.ReviewedAt => sort.Descending
                ? pairs.OrderByDescending(x => x.LastReviewedAt)
                : pairs.OrderBy(x => x.LastReviewedAt),
            ReviewSortField.Decision => sort.Descending
                ? pairs.OrderByDescending(x => x.HasReject)
                : pairs.OrderBy(x => x.HasReject),
            ReviewSortField.ScriptId => sort.Descending
                ? pairs.OrderByDescending(x => x.ScriptId)
                : pairs.OrderBy(x => x.ScriptId),
            _ => pairs.OrderByDescending(x => x.LastReviewedAt)
        };

        var page = await ordered
            .ThenBy(x => x.ScriptId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var pageScripts = page.Select(x => x.ScriptId).ToList();

        // Kéo MỌI lần thu của các cặp trong trang — cả hai biến thể lẫn các bản cũ để chọn bản
        // mới nhất — kèm lượt duyệt và lý do từ chối của từng lượt.
        var rows = await db.Recordings.AsNoTracking()
            .Include(r => r.Script)
            .Include(r => r.Speaker)
            .Include(r => r.Task)
            .Include(r => r.Reviews).ThenInclude(v => v.RejectionReasons).ThenInclude(rr => rr.Reason)
            .Where(r => pageScripts.Contains(r.ScriptId))
            .OrderByDescending(r => r.RecordedAt)
            .ToListAsync(ct);

        // Mỗi biến thể lấy lần thu mới nhất; link nghe cấp sau khi đã lấy đủ dòng, và mỗi
        // bản một lần gọi kho lưu trữ — bản trỏ sang kho khác thì để null chứ không làm
        // hỏng cả dòng lịch sử.
        var rowsByScript = rows.ToLookup(r => r.ScriptId);
        var items = new List<ReviewerHistoryItemDto>(page.Count);

        // Dựng dòng theo đúng thứ tự đã cắt trang, KHÔNG theo thứ tự của `rows` — `rows` xếp
        // theo thời điểm thu, nên `?sort=decision` hay `?sort=scriptId` sẽ trả về thứ tự khác
        // hẳn thứ tự client yêu cầu.
        foreach (var pair in page)
        {
            var takes = rowsByScript[pair.ScriptId];
            var cs = takes.FirstOrDefault(r => r.SentenceVariant == SentenceVariant.CodeSwitching);
            var vi = takes.FirstOrDefault(r => r.SentenceVariant == SentenceVariant.PureVietnamese);

            // Cặp có mặt trong lịch sử nghĩa là tôi đã duyệt một bản của nó, nên chắc chắn có ít nhất một bản.
            var take = (cs ?? vi)!;
            var both = new[] { cs, vi }.OfType<Recording>().ToArray();

            items.Add(new ReviewerHistoryItemDto(
                pair.ScriptId,
                take.SpeakerId,
                take.Speaker.FullName,
                take.Task?.Description,
                take.Script.CsContent,
                take.Script.ViContent,
                cs?.RecordingId,
                vi?.RecordingId,
                await SignAsync(cs, ct),
                await SignAsync(vi, ct),
                cs?.DurationSec ?? vi?.DurationSec ?? 0,
                (pair.HasReject == 1 ? ReviewDecision.Rejected : ReviewDecision.Approved).ToString(),
                // Trạng thái của cặp, cùng luật với lịch sử ghi âm của Speaker: có bản bị từ
                // chối thì cặp đó từ chối; đủ cả hai bản đạt thì mới là đạt; trượt QC hết thì
                // báo lỗi chất lượng; còn lại là đang chờ.
                both.Any(r => r.Status == RecordingStatus.Rejected)
                    ? RecordingStatus.Rejected
                    : cs?.Status == RecordingStatus.Approved && vi?.Status == RecordingStatus.Approved
                        ? RecordingStatus.Approved
                        : both.Length > 0 && both.All(r => r.Status == RecordingStatus.QcFailed)
                            ? RecordingStatus.QcFailed
                            : RecordingStatus.PendingReview,
                pair.LastReviewedAt,
                // Giống GET /api/recordings/{id}/reviews: chỉ hiện lượt duyệt khi bản đã chốt.
                [.. both
                    .Where(r => r.Status is RecordingStatus.Approved or RecordingStatus.Rejected)
                    .SelectMany(r => r.Reviews)
                    .GroupBy(v => v.ReviewRound)
                    .Select(g => g.First())
                    .OrderBy(v => v.ReviewRound)
                    .Select(v => new HistoryReviewDto(
                        v.ReviewRound, v.Decision.ToString(), ReviewDisplay.ReasonOf(v)))]));
        }

        return new PagedResult<ReviewerHistoryItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<PagedResult<ReviewerScriptHistoryItemDto>> GetScriptHistoryAsync(
        long reviewerId, ReviewerScriptHistoryQuery query, CancellationToken ct = default)
    {
        var q = db.Set<ScriptReview>().AsNoTracking()
            .Include(r => r.Script).ThenInclude(s => s.Words)
            .Include(r => r.Script).ThenInclude(s => s.Creator)
            .Include(r => r.ErrorReason)
            .Where(r => r.UserId == reviewerId);

        if (!string.IsNullOrWhiteSpace(query.Kind))
        {
            var kind = query.Kind.Trim().ToLowerInvariant();
            q = kind switch
            {
                "edit" => q.Where(r => r.Action == ScriptReviewAction.Edited),
                "report" => q.Where(r => r.Action == ScriptReviewAction.Rejected),
                "contribution" => q.Where(r => r.Action == ScriptReviewAction.Accepted),
                _ => q
            };
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<ScriptStatus>(query.Status.Trim(), true, out var status))
        {
            q = q.Where(r => r.Script.Status == status);
        }

        var domain = ParseCategory(query.Category);
        if (domain.HasValue) q = q.Where(r => r.Script.Domain == domain.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search.Trim();
            q = q.Where(r => r.ScriptId.Contains(needle)
                || r.Script.CsContent.Contains(needle)
                || r.Script.ViContent.Contains(needle));
        }

        var total = await q.CountAsync(ct);

        var sort = query.ToSort<ScriptReviewSortField>();

        // TEAM_012: nhánh cuối là thứ tự cũ (lượt duyệt mới nhất trước).
        var ordered = sort.Field switch
        {
            ScriptReviewSortField.ReviewedAt => sort.Descending
                ? q.OrderByDescending(r => r.ReviewedAt)
                : q.OrderBy(r => r.ReviewedAt),
            ScriptReviewSortField.Action => sort.Descending
                ? q.OrderByDescending(r => r.Action)
                : q.OrderBy(r => r.Action),
            ScriptReviewSortField.Status => sort.Descending
                ? q.OrderByDescending(r => r.Script.Status)
                : q.OrderBy(r => r.Script.Status),
            ScriptReviewSortField.Domain => sort.Descending
                ? q.OrderByDescending(r => r.Script.Domain)
                : q.OrderBy(r => r.Script.Domain),
            _ => q.OrderByDescending(r => r.ReviewedAt)
        };

        var rows = await ordered
            .ThenBy(r => r.ScriptReviewId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = rows.Select(r => new ReviewerScriptHistoryItemDto(
            r.ScriptId,
            r.Action switch
            {
                ScriptReviewAction.Edited => "edit",
                ScriptReviewAction.Rejected => "report",
                _ => "contribution"
            },
            DomainName(r.Script.Domain),
            r.Script.CsContent,
            r.Script.ViContent,
            // Từ tiếng Anh kèm nghĩa tiếng Việt — cùng dạng với lịch sử đóng góp của Speaker.
            [.. r.Script.Words.OrderBy(w => w.WordPosition).Select(w => new AlignmentItem
            {
                Source = w.EnWord,
                SourceLang = "en",
                Target = w.ViWord,
                TargetLang = "vi",
                Relation = w.Relation == ScriptWordRelation.ProperNoun ? "proper_noun" : "semantic_equivalent"
            })],
            r.Script.CreatedBy,
            r.Script.Creator.FullName,
            r.Action.ToString(),
            r.ErrorReason?.Description ?? r.ErrorReason?.ReasonCode,
            r.Comment,
            r.Script.Status.ToString(),
            r.ReviewedAt)).ToList();

        return new PagedResult<ReviewerScriptHistoryItemDto>(items, query.Page, query.PageSize, total);
    }

    /// <summary>Link nghe tạm của một bản ghi; hỏng thì để null chứ không làm hỏng cả dòng lịch sử.</summary>
    private async Task<string?> SignAsync(Recording? rec, CancellationToken ct)
    {
        if (rec is null) return null;
        try
        {
            var key = storage.GetObjectKey(rec.CloudLink);
            return key is null ? null : (await storage.GetDownloadUrlAsync(key, ct)).Url;
        }
        catch { return null; }
    }

    private static string CategoryLabel(RejectionCategory category) => category switch
    {
        RejectionCategory.Pronunciation => "Phát âm sai (Code-Switching)",
        RejectionCategory.AudioQuality => "Tạp âm / Tiếng ồn môi trường",
        RejectionCategory.Content => "Đọc thiếu / Sai văn bản",
        _ => "Khác",
    };

    private static string DomainName(ScriptDomain domain) => domain switch
    {
        ScriptDomain.ItTechnology => "Công nghệ thông tin",
        ScriptDomain.Education => "Giáo dục",
        _ => "Hội thoại hàng ngày",
    };

    private static ScriptDomain? ParseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;
        var c = category.Trim().ToLowerInvariant();
        if (c.Contains("công nghệ") || c.Contains("information") || c.Contains("it")) return ScriptDomain.ItTechnology;
        if (c.Contains("giáo dục") || c.Contains("education")) return ScriptDomain.Education;
        if (c.Contains("hàng ngày") || c.Contains("daily") || c.Contains("hội thoại")) return ScriptDomain.DailyLife;
        return null;
    }
}
