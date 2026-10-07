using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Services.Implementations;

public class ReviewerStatsService(CodeSwitchLabelDbContext db) : IReviewerStatsService
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
                || v.Recording!.Script.CsContent.Contains(needle)
                || v.Recording!.Speaker.FullName.Contains(needle));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(v => v.ReviewedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = rows.Select(v => new ReviewerHistoryItemDto(
            v.RecordingId,
            v.Recording!.ScriptId,
            v.Recording.SpeakerId,
            v.Recording.Speaker.FullName,
            v.Task?.Description,
            CodeSwitchText.Strip(
                v.Recording.SentenceVariant == SentenceVariant.CodeSwitching
                    ? v.Recording.Script.CsContent
                    : v.Recording.Script.ViContent),
            v.Recording.DurationSec,
            v.Decision.ToString(),
            v.Recording.Status.ToString(),
            v.ReviewedAt)).ToList();

        return new PagedResult<ReviewerHistoryItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<PagedResult<ReviewerScriptHistoryItemDto>> GetScriptHistoryAsync(
        long reviewerId, ReviewerScriptHistoryQuery query, CancellationToken ct = default)
    {
        var q = db.Set<Repositories.Entities.ScriptReview>().AsNoTracking()
            .Include(r => r.Script)
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

        var rows = await q
            .OrderByDescending(r => r.ReviewedAt)
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
            r.Action.ToString(),
            r.Script.Status.ToString(),
            r.ReviewedAt)).ToList();

        return new PagedResult<ReviewerScriptHistoryItemDto>(items, query.Page, query.PageSize, total);
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
