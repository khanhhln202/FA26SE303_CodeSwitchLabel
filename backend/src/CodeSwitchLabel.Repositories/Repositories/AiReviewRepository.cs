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

/// <summary>
/// Một dòng trong hàng đợi chờ máy phán. Kèm sẵn văn bản và hai lượt duyệt của người để
/// giao diện dựng được bảng chỉ bằng một lần gọi, thay vì gọi thêm một lần cho mỗi bản ghi.
/// </summary>
public record AiReviewPendingItem(
    string RecordingId,
    SentenceVariant Variant,
    decimal DurationSec,
    string ExpectedRaw,
    DateTimeOffset RecordedAt,
    List<AiReviewPendingVote> Reviews);

/// <summary>Một lượt duyệt của người trên bản ghi đang chờ máy.</summary>
public record AiReviewPendingVote(
    short Round,
    long ReviewerId,
    ReviewDecision Decision,
    DateTimeOffset ReviewedAt);

/// <summary>
/// Số lượt duyệt đã có của một bản ghi. <paramref name="Approved"/> là số phiếu đạt,
/// nên số phiếu không đạt là <c>Total - Approved</c>.
/// </summary>
public record ReviewTally(int Total, int Approved)
{
    /// <summary>
    /// Đúng thế cần máy phá: hai lượt người và hai lượt đó ngược nhau.
    ///
    /// Trigger trg_review_majority đã chốt mọi ca hai người CÙNG kết quả ngay ở lượt 2,
    /// nên bản ghi còn chờ duyệt mà có đúng 2 lượt thì chỉ có thể là 1–1. Vẫn kiểm cả
    /// hai điều kiện để lỗi trả về nói rõ lý do, thay vì dựa vào suy luận đó.
    /// </summary>
    public bool IsTie => Total == 2 && Approved == 1;
}

public interface IAiReviewRepository
{
    /// <summary>Null nghĩa là migration V24 chưa chạy trên database này.</summary>
    Task<long?> GetAiReviewerIdAsync(CancellationToken ct = default);

    Task<AiReviewCandidate?> GetCandidateAsync(string recordingId, CancellationToken ct = default);

    /// <summary>
    /// Các bản ghi ĐANG LỆCH mà máy chưa phán, cũ nhất trước. Lọc sẵn ở database để lượt
    /// chạy lô không gọi ASR cho bản chưa tới lượt máy — mỗi lượt gọi là một lần tốn tiền.
    /// </summary>
    Task<List<string>> GetTiedRecordingIdsAsync(
        int limit, long aiReviewerId, CancellationToken ct = default);

    /// <summary>
    /// Cùng tập bản ghi với <see cref="GetTiedRecordingIdsAsync"/>, nhưng kèm dữ liệu để
    /// hiển thị. Hai hàm dùng CHUNG một bộ điều kiện, nên danh sách không bao giờ chứa
    /// bản ghi mà lượt chạy lô bỏ qua.
    /// </summary>
    Task<List<AiReviewPendingItem>> GetPendingAsync(
        int limit, long aiReviewerId, CancellationToken ct = default);

    Task<bool> HasVotedAsync(string recordingId, long aiReviewerId, CancellationToken ct = default);

    /// <summary>
    /// Số lượt duyệt đã có và số phiếu đạt. Dùng để kiểm đã tới lượt máy chưa, và để tính
    /// ExpectedRound mà SubmitAsync bắt buộc phải khớp.
    /// </summary>
    Task<ReviewTally> GetReviewTallyAsync(string recordingId, CancellationToken ct = default);

    Task<AiReview?> GetLatestAsync(string recordingId, CancellationToken ct = default);

    void Add(AiReview row);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Mở transaction để lượt bỏ phiếu và dòng bằng chứng cùng vào hoặc cùng không vào.
    /// ReviewService.SubmitAsync tự nhập vào transaction đang mở thay vì mở cái mới.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
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

    /// <summary>
    /// "Đã tới lượt máy": đúng hai lượt duyệt của người, hai lượt đó khác kết quả nhau, và
    /// máy chưa từng phán bản này.
    /// <para>
    /// Viết một lần rồi dùng chung cho cả danh sách lẫn lượt chạy lô. Để hai chỗ tự chép lại
    /// bộ điều kiện này thì sớm muộn chúng lệch nhau, và giao diện sẽ hiện những bản ghi mà
    /// lượt chạy lô không bao giờ đụng tới — kiểu lỗi rất khó lần ra.
    /// </para>
    /// </summary>
    private IQueryable<Recording> TiedRecordings(long aiReviewerId) =>
        db.Recordings
            .Where(r => r.Status == RecordingStatus.PendingReview)
            .Where(r => r.SpeakerId != aiReviewerId)
            .Where(r => !db.AiReviews.Any(a => a.RecordingId == r.RecordingId))
            .Where(r => db.Reviews.Count(v => v.RecordingId == r.RecordingId) == 2)
            .Where(r => db.Reviews.Count(
                v => v.RecordingId == r.RecordingId && v.Decision == ReviewDecision.Approved) == 1)
            .OrderBy(r => r.RecordedAt);

    public Task<List<string>> GetTiedRecordingIdsAsync(
        int limit, long aiReviewerId, CancellationToken ct = default) =>
        TiedRecordings(aiReviewerId)
            .Select(r => r.RecordingId)
            .Take(limit)
            .ToListAsync(ct);

    public Task<List<AiReviewPendingItem>> GetPendingAsync(
        int limit, long aiReviewerId, CancellationToken ct = default) =>
        TiedRecordings(aiReviewerId)
            .Take(limit)
            .Select(r => new AiReviewPendingItem(
                r.RecordingId,
                r.SentenceVariant,
                r.DurationSec,
                db.Scripts
                    .Where(s => s.ScriptId == r.ScriptId)
                    .Select(s => r.SentenceVariant == SentenceVariant.CodeSwitching
                        ? s.CsContent
                        : s.ViContent)
                    .First(),
                r.RecordedAt,
                db.Reviews
                    .Where(v => v.RecordingId == r.RecordingId)
                    .OrderBy(v => v.ReviewRound)
                    .Select(v => new AiReviewPendingVote(
                        v.ReviewRound, v.ReviewerId, v.Decision, v.ReviewedAt))
                    .ToList()))
            .ToListAsync(ct);

    public Task<bool> HasVotedAsync(string recordingId, long aiReviewerId, CancellationToken ct = default) =>
        db.Reviews.AnyAsync(v => v.RecordingId == recordingId && v.ReviewerId == aiReviewerId, ct);

    public async Task<ReviewTally> GetReviewTallyAsync(
        string recordingId, CancellationToken ct = default)
    {
        var rows = await db.Reviews
            .Where(v => v.RecordingId == recordingId)
            .Select(v => v.Decision)
            .ToListAsync(ct);

        return new ReviewTally(rows.Count, rows.Count(d => d == ReviewDecision.Approved));
    }

    public Task<AiReview?> GetLatestAsync(string recordingId, CancellationToken ct = default) =>
        db.AiReviews
            .Where(a => a.RecordingId == recordingId)
            .OrderByDescending(a => a.AnalyzedAt)
            .ThenByDescending(a => a.AiReviewId)
            .FirstOrDefaultAsync(ct);

    public void Add(AiReview row) => db.AiReviews.Add(row);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);
}
