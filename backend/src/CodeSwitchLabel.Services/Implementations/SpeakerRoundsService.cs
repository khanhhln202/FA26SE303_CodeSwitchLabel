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
            .Where(a => a.Task.Status == WorkTaskStatus.InProgress)
            .OrderBy(a => a.Task.Deadline)
            .Select(a => a.Task)
            .FirstOrDefaultAsync(ct);

        long? campaignId = task?.CampaignId;

        // Không đăng ký chiến dịch nào cũng ghi âm được — việc thu dẫn ra từ task được giao.
        // Nên "đợt đang làm" cũng lấy theo task: không có task thì không có đợt, thay vì dựng
        // một đợt từ việc đăng ký mà việc đăng ký không còn là điều kiện gì.
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

        // TEAM_013: đợt chưa có nhiệm vụ nào (Open) thì người đọc còn đăng ký được;
        // đợt đã có nhiệm vụ rồi thì việc đến từ task, không phải từ đăng ký.
        // Trước đây lọc cả `Draft` — trạng thái đó không tồn tại nữa, và đợt mới tạo đã là Open.
        var campaigns = await db.Campaigns.AsNoTracking()
            .Where(c => c.Status == CampaignStatus.Open)
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
            HumanizeDays((c.StartDate.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days),
            null,
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

        // TEAM_013: đợt chỉ còn Open/InProgress/Completed, và trạng thái đợt là hàm của task —
        // đăng ký là việc của người đọc chứ không phải việc của task, nên đợt đang chạy hay
        // đã xong đều không chặn. Luật cũ chặn cả Completed lẫn Cancelled đã bị gỡ.
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

        // Một dòng là MỘT CẶP bản ghi, nên phân trang theo cặp chứ không theo bản ghi:
        // gom scriptId theo mốc bản ghi mới nhất rồi mới kéo bản ghi của đúng các cặp đó.
        // (Distinct + OrderBy RecordedAt thì Postgres không nhận, nên gom bằng GroupBy/Max.)
        var total = await q.Select(r => r.ScriptId).Distinct().CountAsync(ct);

        var pairs = q
            .GroupBy(r => r.ScriptId)
            .Select(g => new { ScriptId = g.Key, Last = g.Max(r => r.RecordedAt) });

        var sort = query.ToSort<SpeakerPairSortField>();

        // TEAM_012: chỉ hai cột của cặp là sắp xếp được trước khi cắt trang. Trạng thái và
        // thời lượng của cặp là thứ TÍNH RA ở dưới, nên không có trong SpeakerPairSortField.
        var orderedPairs = sort.Field switch
        {
            SpeakerPairSortField.ScriptId => sort.Descending
                ? pairs.OrderByDescending(x => x.ScriptId)
                : pairs.OrderBy(x => x.ScriptId),
            SpeakerPairSortField.LastRecordedAt => sort.Descending
                ? pairs.OrderByDescending(x => x.Last)
                : pairs.OrderBy(x => x.Last),
            _ => pairs.OrderByDescending(x => x.Last)
        };

        var pageScripts = await orderedPairs
            .ThenBy(x => x.ScriptId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => x.ScriptId)
            .ToListAsync(ct);

        var rows = await q
            .Where(r => pageScripts.Contains(r.ScriptId))
            .OrderByDescending(r => r.RecordedAt)
            .ToListAsync(ct);

        // Mỗi biến thể lấy lần thu mới nhất; link nghe cấp sau khi đã lấy đủ dòng, và mỗi
        // bản một lần gọi kho lưu trữ — bản trỏ sang kho khác thì để null chứ không làm
        // hỏng cả dòng lịch sử.
        var items = new List<SpeakerRecordingHistoryDto>(pageScripts.Count);

        // TEAM_012: dựng dòng theo đúng thứ tự pageScripts đã cắt trang, KHÔNG theo thứ tự
        // của `rows` — `rows` xếp theo thời điểm thu, nên `?sort=scriptId` sẽ trả về thứ tự
        // khác hẳn thứ tự client yêu cầu.
        var rowsByScript = rows.ToLookup(r => r.ScriptId);

        foreach (var scriptId in pageScripts)
        {
            var pair = rowsByScript[scriptId];
            var cs = pair.Where(r => r.SentenceVariant == SentenceVariant.CodeSwitching)
                .OrderByDescending(r => r.RecordedAt).FirstOrDefault();
            var vi = pair.Where(r => r.SentenceVariant == SentenceVariant.PureVietnamese)
                .OrderByDescending(r => r.RecordedAt).FirstOrDefault();

            var takes = new[] { cs, vi }.OfType<Recording>().ToArray();

            items.Add(new SpeakerRecordingHistoryDto(
                scriptId,
                (cs ?? vi)?.Task?.Description,
                (cs ?? vi)!.Script.CsContent,
                (cs ?? vi)!.Script.ViContent,
                cs?.RecordingId,
                vi?.RecordingId,
                await SignAsync(cs, ct),
                await SignAsync(vi, ct),
                cs?.DurationSec ?? vi?.DurationSec ?? 0,
                // Trạng thái của cặp: có bản bị từ chối thì cặp đó từ chối; đủ cả hai bản
                // đạt thì mới là đạt; trượt QC hết thì báo lỗi chất lượng; còn lại là đang
                // chờ (thiếu bản, hoặc đã nộp chưa có kết quả).
                takes.Any(r => r.Status == RecordingStatus.Rejected)
                    ? RecordingStatus.Rejected
                    : cs?.Status == RecordingStatus.Approved && vi?.Status == RecordingStatus.Approved
                        ? RecordingStatus.Approved
                        : takes.Length > 0 && takes.All(r => r.Status == RecordingStatus.QcFailed)
                            ? RecordingStatus.QcFailed
                            : RecordingStatus.PendingReview,
                takes.Max(r => r.RecordedAt),
                // Giống GET /api/recordings/{id}/reviews: chỉ hiện lượt duyệt khi bản đã chốt.
                // Hai bản có thể chốt ở hai trạng thái khác nhau, nên mỗi lượt tính một lần.
                [.. takes
                    .Where(r => r.Status is RecordingStatus.Approved or RecordingStatus.Rejected)
                    .SelectMany(r => r.Reviews)
                    .GroupBy(v => v.ReviewRound)
                    .Select(g => g.First())
                    .OrderBy(v => v.ReviewRound)
                    .Select(v => new HistoryReviewDto(v.ReviewRound, v.Decision.ToString(), v.Comment))]));
        }

        return new PagedResult<SpeakerRecordingHistoryDto>(items, query.Page, query.PageSize, total);
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

        var sort = query.ToSort<ScriptSortField>();

        // TEAM_012: cùng bộ cột với GET /api/scripts vì hai danh sách này đọc cùng bảng.
        var ordered = sort.Field switch
        {
            ScriptSortField.ScriptId => sort.Descending
                ? q.OrderByDescending(s => s.ScriptId)
                : q.OrderBy(s => s.ScriptId),
            ScriptSortField.CreatedAt => sort.Descending
                ? q.OrderByDescending(s => s.CreatedAt)
                : q.OrderBy(s => s.CreatedAt),
            ScriptSortField.Domain => sort.Descending
                ? q.OrderByDescending(s => s.Domain)
                : q.OrderBy(s => s.Domain),
            ScriptSortField.Status => sort.Descending
                ? q.OrderByDescending(s => s.Status)
                : q.OrderBy(s => s.Status),
            _ => q.OrderByDescending(s => s.CreatedAt)
        };

        var rows = await ordered
            .ThenBy(s => s.ScriptId)
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
        var recordings = await db.Recordings.AsNoTracking()
            .Where(r => r.SpeakerId == speakerId)
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byRecordingStatus = recordings.ToDictionary(x => x.Key, x => x.Count);
        var approved = byRecordingStatus.GetValueOrDefault(RecordingStatus.Approved);
        var rejected = byRecordingStatus.GetValueOrDefault(RecordingStatus.Rejected);
        var pending = byRecordingStatus.GetValueOrDefault(RecordingStatus.PendingReview)
            + byRecordingStatus.GetValueOrDefault(RecordingStatus.QcFailed);

        // Câu do chính người đọc nộp: cùng cách gom theo trạng thái như bản ghi.
        var scripts = await db.Scripts.AsNoTracking()
            .Where(s => s.CreatedBy == speakerId)
            .GroupBy(s => s.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byScriptStatus = scripts.ToDictionary(x => x.Key, x => x.Count);
        var validated = byScriptStatus.GetValueOrDefault(ScriptStatus.Validated);
        var scriptsRejected = byScriptStatus.GetValueOrDefault(ScriptStatus.Rejected)
            + byScriptStatus.GetValueOrDefault(ScriptStatus.Deactivated);
        var scriptsPending = byScriptStatus.GetValueOrDefault(ScriptStatus.PendingValidation);

        return new SpeakerStatsDto(
            new RecordingStats(approved + rejected + pending, approved, rejected, pending),
            new ContributionStats(
                validated + scriptsRejected + scriptsPending, validated, scriptsRejected, scriptsPending));
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
