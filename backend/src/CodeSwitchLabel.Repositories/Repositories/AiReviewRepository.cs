using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeSwitchLabel.Repositories.Repositories;

/// <summary>
/// Một bản ghi sẵn sàng cho máy phán, kèm sẵn đoạn văn bản đáng ra phải đọc.
///
/// <paramref name="ExpectedRaw"/> lấy từ script.cs_content với bản code_switching và
/// script.vi_content với bản pure_vietnamese — vẫn còn nhãn [vi]/[en], tầng Services
/// mới bỏ nhãn và chuẩn hoá.
/// </summary>
public record AiReviewCandidate(
    string RecordingId,
    SentenceVariant Variant,
    RecordingStatus Status,
    string CloudLink,
    string ExpectedRaw,
    long SpeakerId);

public interface IAiReviewRepository
{
    /// <summary>Null nghĩa là migration V23 chưa chạy trên database này.</summary>
    Task<long?> GetAiReviewerIdAsync(CancellationToken ct = default);

    Task<AiReviewCandidate?> GetCandidateAsync(string recordingId, CancellationToken ct = default);

    /// <summary>
    /// Các bản còn chờ duyệt mà máy CHƯA phán, cũ nhất trước. Lọc sẵn ở database để lượt
    /// chạy lô không gọi ASR hai lần cho cùng một bản — mỗi lượt gọi là một lần tốn tiền.
    /// </summary>
    Task<List<string>> GetPendingRecordingIdsAsync(
        int limit, long aiReviewerId, CancellationToken ct = default);

    Task<bool> HasVotedAsync(string recordingId, long aiReviewerId, CancellationToken ct = default);

    /// <summary>
    /// Số lượt duyệt đã có. Cần để tính ExpectedRound mà SubmitAsync bắt buộc phải khớp —
    /// nó là lưới chặn người đến sau ghi quyết định sang vòng khác.
    /// </summary>
    Task<int> GetReviewCountAsync(string recordingId, CancellationToken ct = default);

    /// <summary>
    /// Mở transaction để lượt bỏ phiếu và dòng bằng chứng cùng vào hoặc cùng không vào.
    /// ReviewService.SubmitAsync tự nhập vào transaction đang mở thay vì mở cái mới.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    Task<AiReview?> GetLatestAsync(string recordingId, CancellationToken ct = default);

    void Add(AiReview row);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public class AiReviewRepository(CodeSwitchLabelDbContext db) : IAiReviewRepository
{
    public async Task<long?> GetAiReviewerIdAsync(CancellationToken ct = default)
    {
        var id = await db.AppUsers
            .Where(u => u.Email == AiReviewer.Email)
            .Select(u => (long?)u.UserId)
            .FirstOrDefaultAsync(ct);

        return id;
    }

    public Task<AiReviewCandidate?> GetCandidateAsync(string recordingId, CancellationToken ct = default) =>
        db.Recordings
            .Where(r => r.RecordingId == recordingId)
            .Join(db.Scripts, r => r.ScriptId, s => s.ScriptId, (r, s) => new AiReviewCandidate(
                r.RecordingId,
                r.SentenceVariant,
                r.Status,
                r.CloudLink,
                r.SentenceVariant == SentenceVariant.CodeSwitching ? s.CsContent : s.ViContent,
                r.SpeakerId))
            .FirstOrDefaultAsync(ct);

    public Task<List<string>> GetPendingRecordingIdsAsync(
        int limit, long aiReviewerId, CancellationToken ct = default) =>
        db.Recordings
            .Where(r => r.Status == RecordingStatus.PendingReview)
            .Where(r => r.SpeakerId != aiReviewerId)
            .Where(r => !db.AiReviews.Any(a => a.RecordingId == r.RecordingId))
            .OrderBy(r => r.RecordedAt)
            .Select(r => r.RecordingId)
            .Take(limit)
            .ToListAsync(ct);

    public Task<bool> HasVotedAsync(string recordingId, long aiReviewerId, CancellationToken ct = default) =>
        db.Reviews.AnyAsync(v => v.RecordingId == recordingId && v.ReviewerId == aiReviewerId, ct);

    public Task<int> GetReviewCountAsync(string recordingId, CancellationToken ct = default) =>
        db.Reviews.CountAsync(v => v.RecordingId == recordingId, ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);

    public Task<AiReview?> GetLatestAsync(string recordingId, CancellationToken ct = default) =>
        db.AiReviews
            .Where(a => a.RecordingId == recordingId)
            .OrderByDescending(a => a.AnalyzedAt)
            .ThenByDescending(a => a.AiReviewId)
            .FirstOrDefaultAsync(ct);

    public void Add(AiReview row) => db.AiReviews.Add(row);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
