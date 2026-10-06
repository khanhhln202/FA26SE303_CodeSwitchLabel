using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Services.Implementations;

public class SpeakerRoundsService(
    CodeSwitchLabelDbContext db,
    IObjectStorage storage,
    TimeProvider clock) : ISpeakerRoundsService
{
    public async Task<SpeakerRoundDto?> GetCurrentAsync(long speakerId, CancellationToken ct = default)
    {
        // Đợt đang làm = chiến dịch của task thu âm đang giao cho mình, hạn gần nhất trước.
        var task = await db.TaskAssignments.AsNoTracking()
            .Where(a => a.UserId == speakerId && a.AssignmentStatus == AssignmentStatus.Active)
            .Where(a => a.Task.TaskType == TaskType.Recording)
            .Where(a => a.Task.Status == WorkTaskStatus.Open || a.Task.Status == WorkTaskStatus.InProgress)
            .OrderBy(a => a.Task.Deadline)
            .Select(a => a.Task)
            .FirstOrDefaultAsync(ct);

        long? campaignId = task?.CampaignId;

        // Chưa có task thì lấy đợt đã đăng ký còn đang chạy.
        if (campaignId is null)
        {
            campaignId = await db.CampaignRegistrations.AsNoTracking()
                .Where(r => r.SpeakerId == speakerId)
                .Where(r => r.Campaign.Status == CampaignStatus.Open || r.Campaign.Status == CampaignStatus.InProgress)
                .OrderBy(r => r.Campaign.EndDate)
                .Select(r => (long?)r.CampaignId)
                .FirstOrDefaultAsync(ct);
        }

        if (campaignId is null) return null;

        return await BuildRoundAsync(campaignId.Value, speakerId, ct);
    }

    public async Task<IReadOnlyList<UpcomingRoundDto>> GetUpcomingAsync(
        long speakerId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var mine = await db.CampaignRegistrations.AsNoTracking()
            .Where(r => r.SpeakerId == speakerId)
            .Select(r => r.CampaignId)
            .ToListAsync(ct);

        var campaigns = await db.Campaigns.AsNoTracking()
            .Where(c => c.Status == CampaignStatus.Draft || c.Status == CampaignStatus.Open)
            .Where(c => c.EndDate >= today)
            .OrderBy(c => c.StartDate)
            .Take(3)
            .ToListAsync(ct);

        return [.. campaigns.Select(c => new UpcomingRoundDto(
            c.CampaignId,
            c.CampaignName,
            null,
            c.TargetQty,
            $"{c.StartDate:dd/MM} – {c.EndDate:dd/MM}",
            c.Status == CampaignStatus.Open
                ? HumanizeDays((c.StartDate.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days)
                : null,
            c.Status == CampaignStatus.Draft ? c.StartDate.ToString("dd/MM") : null,
            mine.Contains(c.CampaignId)))];
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
        long campaignId, long speakerId, int top, CancellationToken ct = default)
    {
        if (!await db.Campaigns.AsNoTracking().AnyAsync(c => c.CampaignId == campaignId, ct))
        {
            throw new NotFoundException("campaign_not_found", $"Không tìm thấy chiến dịch #{campaignId}.");
        }

        var rows = await db.Recordings.AsNoTracking()
            .Where(r => r.Status == RecordingStatus.Approved)
            .Where(r => r.Task != null && r.Task.CampaignId == campaignId)
            .GroupBy(r => new { r.SpeakerId, r.Speaker.FullName })
            .Select(g => new { g.Key.SpeakerId, g.Key.FullName, Approved = g.Count() })
            .OrderByDescending(x => x.Approved)
            .Take(Math.Clamp(top, 1, 50))
            .ToListAsync(ct);

        return [.. rows.Select((x, i) => new LeaderboardEntryDto(
            i + 1, x.SpeakerId, x.FullName, x.Approved, x.SpeakerId == speakerId))];
    }

    public async Task<SpeakerRoundDto> RegisterAsync(
        long campaignId, long speakerId, CancellationToken ct = default)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.CampaignId == campaignId, ct)
            ?? throw new NotFoundException("campaign_not_found", $"Không tìm thấy chiến dịch #{campaignId}.");

        if (campaign.Status is CampaignStatus.Completed or CampaignStatus.Cancelled)
        {
            throw new UnprocessableException(
                "campaign_not_accepting_tasks",
                $"Chiến dịch #{campaignId} đang ở trạng thái {campaign.Status} nên không nhận đăng ký.");
        }

        var exists = await db.CampaignRegistrations.AnyAsync(
            r => r.CampaignId == campaignId && r.SpeakerId == speakerId, ct);

        if (exists)
        {
            throw new ConflictException(
                "already_registered", $"Bạn đã đăng ký chiến dịch #{campaignId} rồi.");
        }

        db.CampaignRegistrations.Add(new CampaignRegistration
        {
            CampaignId = campaignId,
            SpeakerId = speakerId,
            RegisteredAt = clock.GetUtcNow()
        });
        await db.SaveChangesAsync(ct);

        return (await BuildRoundAsync(campaignId, speakerId, ct))!;
    }

    public async Task UnregisterAsync(long campaignId, long speakerId, CancellationToken ct = default)
    {
        var reg = await db.CampaignRegistrations.FirstOrDefaultAsync(
            r => r.CampaignId == campaignId && r.SpeakerId == speakerId, ct)
            ?? throw new NotFoundException(
                "registration_not_found", $"Bạn chưa đăng ký chiến dịch #{campaignId}.");

        db.CampaignRegistrations.Remove(reg);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RejectedRecordingDto>> GetRejectedAsync(
        long speakerId, int limit, CancellationToken ct = default)
    {
        var rows = await db.Recordings.AsNoTracking()
            .Include(r => r.Script)
            .Include(r => r.Reviews).ThenInclude(v => v.RejectionReasons).ThenInclude(rr => rr.Reason)
            .Where(r => r.SpeakerId == speakerId && r.Status == RecordingStatus.Rejected)
            .OrderByDescending(r => r.RecordedAt)
            .Take(Math.Clamp(limit, 1, 20))
            .ToListAsync(ct);

        var result = new List<RejectedRecordingDto>();
        foreach (var r in rows)
        {
            string? url;
            DateTimeOffset expiresAt;
            try
            {
                var key = storage.GetObjectKey(r.CloudLink);
                if (key is null) continue;
                (url, expiresAt) = await storage.GetDownloadUrlAsync(key, ct);
            }
            catch { continue; }

            var reason = r.Reviews
                .SelectMany(v => v.RejectionReasons.Select(rr => rr.Reason.Description ?? rr.Reason.ReasonCode))
                .FirstOrDefault();

            result.Add(new RejectedRecordingDto(
                r.RecordingId, r.ScriptId, r.Script.CsContent, reason, url, expiresAt));
        }

        return result;
    }

    public async Task<PagedResult<SpeakerRecordingHistoryDto>> GetRecordingHistoryAsync(
        long speakerId, SpeakerHistoryQuery query, CancellationToken ct = default)
    {
        var q = db.Recordings.AsNoTracking()
            .Include(r => r.Script)
            .Include(r => r.Task)
            .Include(r => r.Reviews)
            .Where(r => r.SpeakerId == speakerId);

        if (query.Status.HasValue) q = q.Where(r => r.Status == query.Status.Value);
        if (query.TaskId.HasValue) q = q.Where(r => r.TaskId == query.TaskId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search.Trim();
            q = q.Where(r => r.RecordingId.Contains(needle)
                || r.Script.CsContent.Contains(needle)
                || r.Script.ViContent.Contains(needle));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(r => r.RecordedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = rows.Select(r => new SpeakerRecordingHistoryDto(
            r.RecordingId,
            r.ScriptId,
            r.Task?.Description,
            r.Script.CsContent,
            r.Script.ViContent,
            null,
            null,
            r.DurationSec,
            r.Status,
            r.RecordedAt,
            // Giống GET /api/recordings/{id}/reviews: chỉ hiện lượt duyệt khi bản đã chốt.
            r.Status is RecordingStatus.Approved or RecordingStatus.Rejected
                ? [.. r.Reviews.OrderBy(v => v.ReviewRound).Select(v => new HistoryReviewDto(
                    v.ReviewRound,
                    v.Decision.ToString(),
                    v.Comment))]
                : [])).ToList();

        return new PagedResult<SpeakerRecordingHistoryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<PagedResult<SpeakerContributionHistoryDto>> GetContributionHistoryAsync(
        long speakerId, SpeakerContributionHistoryQuery query, CancellationToken ct = default)
    {
        var q = db.Scripts.AsNoTracking()
            .Include(s => s.Words)
            .Include(s => s.Reviews).ThenInclude(r => r.ErrorReason)
            .Where(s => s.CreatedBy == speakerId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search.Trim();
            q = q.Where(s => s.ScriptId.Contains(needle)
                || s.CsContent.Contains(needle)
                || s.ViContent.Contains(needle));
        }

        // Lọc theo tên category tiếng Việt của FE (giữ tương thích mock).
        var domain = ParseCategory(query.Category);
        if (domain.HasValue) q = q.Where(s => s.Domain == domain.Value);

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(s => s.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = rows.Select(s => new SpeakerContributionHistoryDto(
            s.ScriptId,
            DomainName(s.Domain),
            s.CsContent,
            s.ViContent,
            [.. s.Words.OrderBy(w => w.WordPosition).Select(w => new AlignmentItem
            {
                Source = w.EnWord,
                SourceLang = "en",
                Target = w.ViWord,
                TargetLang = "vi",
                Relation = w.Relation == ScriptWordRelation.ProperNoun ? "proper_noun" : "semantic_equivalent"
            })],
            s.CreatedAt,
            s.Status,
            [.. s.Reviews.Select(r => new HistoryReviewDto(0, r.Action.ToString(), r.Comment))])).ToList();

        return new PagedResult<SpeakerContributionHistoryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<SpeakerStatsDto> GetStatsAsync(long speakerId, CancellationToken ct = default)
    {
        var groups = await db.Recordings.AsNoTracking()
            .Where(r => r.SpeakerId == speakerId)
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatus = groups.ToDictionary(x => x.Key, x => x.Count);
        var approved = byStatus.GetValueOrDefault(RecordingStatus.Approved);
        var rejected = byStatus.GetValueOrDefault(RecordingStatus.Rejected);
        var pending = byStatus.GetValueOrDefault(RecordingStatus.PendingReview)
            + byStatus.GetValueOrDefault(RecordingStatus.QcFailed);

        return new SpeakerStatsDto(approved + rejected + pending, approved, rejected, pending);
    }

    // ----------------------------------------------------------------- nội bộ

    private async Task<SpeakerRoundDto?> BuildRoundAsync(
        long campaignId, long speakerId, CancellationToken ct)
    {
        var campaign = await db.Campaigns.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CampaignId == campaignId, ct);

        if (campaign is null) return null;

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var speakerCounts = await db.Recordings.AsNoTracking()
            .Where(r => r.SpeakerId == speakerId)
            .Where(r => r.Task != null && r.Task.CampaignId == campaignId)
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatus = speakerCounts.ToDictionary(x => x.Key, x => x.Count);
        var approved = byStatus.GetValueOrDefault(RecordingStatus.Approved);
        var rejected = byStatus.GetValueOrDefault(RecordingStatus.Rejected);
        var pending = byStatus.GetValueOrDefault(RecordingStatus.PendingReview);
        var submitted = approved + rejected + pending;

        var speakersInCampaign = await db.Recordings.AsNoTracking()
            .Where(r => r.Task != null && r.Task.CampaignId == campaignId)
            .Select(r => r.SpeakerId)
            .Distinct()
            .CountAsync(ct);

        var registered = await db.CampaignRegistrations.AsNoTracking()
            .Where(r => r.CampaignId == campaignId)
            .Select(r => r.SpeakerId)
            .Distinct()
            .CountAsync(ct);

        var isRegistered = await db.CampaignRegistrations.AsNoTracking()
            .AnyAsync(r => r.CampaignId == campaignId && r.SpeakerId == speakerId, ct);

        var topic = await DominantTopicAsync(campaignId, ct);

        return new SpeakerRoundDto(
            campaign.CampaignId,
            campaign.CampaignName,
            topic,
            campaign.Status,
            campaign.StartDate,
            campaign.EndDate,
            HumanizeDays((campaign.EndDate.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days),
            Math.Max(speakersInCampaign, registered),
            campaign.TargetQty,
            submitted,
            approved,
            pending,
            isRegistered);
    }

    private async Task<string?> DominantTopicAsync(long campaignId, CancellationToken ct)
    {
        var top = await db.TaskScripts.AsNoTracking()
            .Where(ts => ts.Task.CampaignId == campaignId)
            .GroupBy(ts => ts.Script.Domain)
            .Select(g => new { Domain = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefaultAsync(ct);

        return top is null ? null : DomainName(top.Domain);
    }

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

    private static string HumanizeDays(int days) => days switch
    {
        < 0 => "Đã kết thúc",
        0 => "Hôm nay",
        1 => "1 ngày",
        _ => $"{days} ngày",
    };
}
