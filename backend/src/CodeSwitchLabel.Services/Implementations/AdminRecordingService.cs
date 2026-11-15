using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Reviews;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Services.Implementations;

/// <summary>
/// Bản ghi âm nhìn từ phía Admin. Dòng là MỘT CẶP (câu, Speaker); trạng thái cặp tính bằng
/// <see cref="ReviewRules.PairOutcome"/> — cùng nguồn luật với lịch sử Reviewer và Speaker.
/// </summary>
public class AdminRecordingService(
    CodeSwitchLabelDbContext db,
    IObjectStorage storage,
    ISystemConfigService config) : IAdminRecordingService
{
    public async Task<PagedResult<AdminRecordingPairDto>> GetPairsAsync(
        PageRequest query, CancellationToken ct = default)
    {
        var total = await db.Recordings.AsNoTracking()
            .Select(r => new { r.ScriptId, r.SpeakerId })
            .Distinct()
            .CountAsync(ct);

        // Cắt trang theo CẶP trước khi kéo bản ghi: mỗi (câu, Speaker) một dòng, mới nhất trước.
        var page = await db.Recordings.AsNoTracking()
            .GroupBy(r => new { r.ScriptId, r.SpeakerId })
            .Select(g => new
            {
                g.Key.ScriptId,
                g.Key.SpeakerId,
                Last = g.Max(r => r.RecordedAt)
            })
            .OrderByDescending(x => x.Last)
            .ThenBy(x => x.ScriptId)
            .ThenBy(x => x.SpeakerId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        if (page.Count == 0)
            return new PagedResult<AdminRecordingPairDto>([], query.Page, query.PageSize, total);

        // Kéo mọi lần thu của các câu trong trang (cả take cũ để biết take nào mới nhất).
        var pageScripts = page.Select(x => x.ScriptId).Distinct().ToList();
        var pageSpeakers = page.Select(x => x.SpeakerId).Distinct().ToList();
        var rows = await db.Recordings.AsNoTracking()
            .Include(r => r.Script)
            .Include(r => r.Speaker)
            .Include(r => r.Task)
            .Include(r => r.Reviews).ThenInclude(v => v.Reviewer)
            .Include(r => r.Reviews).ThenInclude(v => v.RejectionReasons).ThenInclude(rr => rr.Reason)
            .Where(r => pageScripts.Contains(r.ScriptId) && pageSpeakers.Contains(r.SpeakerId))
            .OrderByDescending(r => r.RecordedAt)
            .ToListAsync(ct);

        var roundsRequired = await config.GetIntAsync(
            ConfigKeys.ReviewRoundsRequired, ReviewRules.DefaultRoundsRequired, ct);

        var wanted = page.Select(x => (x.ScriptId, x.SpeakerId)).ToHashSet();
        var rowsByPair = rows
            .Where(r => wanted.Contains((r.ScriptId, r.SpeakerId)))
            .ToLookup(r => (r.ScriptId, r.SpeakerId));

        var items = new List<AdminRecordingPairDto>(page.Count);
        foreach (var key in page)
        {
            var takes = rowsByPair[(key.ScriptId, key.SpeakerId)];
            var cs = takes.FirstOrDefault(r => r.SentenceVariant == SentenceVariant.CodeSwitching);
            var vi = takes.FirstOrDefault(r => r.SentenceVariant == SentenceVariant.PureVietnamese);
            var both = new[] { cs, vi }.OfType<Recording>().ToArray();
            var take = both[0];

            var csStatus = StatusOf(cs, roundsRequired);
            var viStatus = StatusOf(vi, roundsRequired);

            items.Add(new AdminRecordingPairDto(
                $"{key.ScriptId}_{key.SpeakerId}",
                [.. both.Select(r => r.RecordingId)],
                key.ScriptId,
                key.SpeakerId,
                take.Speaker.FullName,
                take.Speaker.Email,
                take.Script.CsContent,
                take.Script.ViContent,
                DomainName(take.Script.Domain),
                take.TaskId,
                take.Task?.Description,
                cs?.RecordingId,
                vi?.RecordingId,
                await SignAsync(cs, ct),
                await SignAsync(vi, ct),
                cs?.DurationSec ?? vi?.DurationSec ?? 0,
                // Tổng thời lượng của CẶP hiện tại: chỉ hai take mới nhất, không cộng take đã bị thay.
                (cs?.DurationSec ?? 0) + (vi?.DurationSec ?? 0),
                ReviewRules.PairOutcome(csStatus, viStatus),
                csStatus,
                viStatus,
                // Phiếu của take mới nhất, CỦA CẢ HAI BIẾN THỂ, gộp lại rồi xếp theo RecordingId.
                // Take cũ đã bị thay thế nên không tính.
                //
                // Người hiển thị PHẢI tách lại theo RecordingId. Gộp chung vào một ô ba vòng là
                // sai: cặp có thể có bản vi đã đủ phiếu từ lâu trong khi bản cs vừa thu lại chưa
                // có phiếu nào, và màn hình sẽ trông như bản vừa thu đã được duyệt sẵn.
                [.. both
                    .SelectMany(r => r.Reviews)
                    .OrderBy(v => v.RecordingId)
                    .ThenBy(v => v.ReviewRound)
                    .Select(v => new AdminReviewDto(
                        v.ReviewRound, v.Decision.ToString(), ReviewDisplay.ReasonOf(v),
                        v.RecordingId, v.ReviewerId, v.Reviewer.FullName, v.ReviewedAt))],
                key.Last));
        }

        return new PagedResult<AdminRecordingPairDto>(items, query.Page, query.PageSize, total);
    }

    /// <summary>
    /// Trạng thái từng bản. Bản còn "chờ" được tính lại từ phiếu để khớp lịch sử Reviewer
    /// khi dữ liệu cũ chưa được trigger chốt.
    /// </summary>
    private static RecordingStatus? StatusOf(Recording? recording, int roundsRequired)
    {
        if (recording is null) return null;
        return recording.Status == RecordingStatus.PendingReview
            ? ReviewRules.Outcome(recording.Reviews.Select(v => v.Decision).ToArray(), roundsRequired)
                ?? RecordingStatus.PendingReview
            : recording.Status;
    }

    /// <summary>Link nghe tạm; hỏng thì để null chứ không làm hỏng cả trang.</summary>
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

    private static string DomainName(string domain) => domain switch
    {
        "ItTechnology" => "Công nghệ thông tin",
        "Education" => "Giáo dục",
        "DailyLife" => "Hội thoại hàng ngày",
        // Chủ đề tự tạo: dùng code làm nhãn cho tới khi FE đọc tên từ /api/config/topics.
        _ => domain,
    };
}
