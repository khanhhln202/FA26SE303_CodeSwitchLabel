using System.Diagnostics;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Reviews;

/// <summary>Loại vòng duyệt, suy ra từ số thứ tự vòng.</summary>
public enum ReviewRoundKind
{
    /// <summary>Vòng 1 — quyết định chính.</summary>
    Primary = 1,

    /// <summary>Vòng 2 — rút mẫu ngẫu nhiên kiểm tra lại, duyệt mù.</summary>
    SpotCheck = 2,

    /// <summary>Vòng 3 — phân xử khi vòng 1 và vòng 2 lệch nhau.</summary>
    Adjudication = 3
}

/// <summary>
/// Luật nhiều vòng duyệt, viết thành hàm thuần — không đụng database, thời gian hay
/// số ngẫu nhiên — để test được từng nhánh một cách chắc chắn.
///
///   Vòng 1: không bị rút mẫu → chốt luôn. Bị rút mẫu → chờ vòng 2.
///   Vòng 2: trùng ý vòng 1 → chốt. Lệch → chờ vòng 3.
///   Vòng 3: chốt theo người phân xử.
///
/// Bản ghi chỉ đổi trạng thái MỘT lần, lúc chốt. Nhờ vậy không bao giờ có chuyện
/// đã duyệt đạt, đã được đưa vào dataset, rồi mới bị vòng kiểm tra lật lại.
///
/// ERD không có cột đánh dấu "bản này đã bị rút mẫu". Không cần: bản còn chờ mà
/// đã có đúng một lượt duyệt thì chắc chắn đã bị rút — không bị rút thì đã chốt ở vòng 1.
/// </summary>
public static class ReviewStateMachine
{
    public const int MaxRound = 3;

    public static ReviewRoundKind KindOf(int round) => round switch
    {
        1 => ReviewRoundKind.Primary,
        2 => ReviewRoundKind.SpotCheck,
        3 => ReviewRoundKind.Adjudication,
        _ => throw new ArgumentOutOfRangeException(nameof(round), round, "Vòng duyệt chỉ từ 1 đến 3.")
    };

    /// <summary>
    /// Chỉ vòng kiểm tra là mù. Vòng 1 không có gì để giấu;
    /// vòng phân xử thì phải thấy cả hai ý kiến mới phân xử được.
    /// </summary>
    public static bool IsBlind(int round) => KindOf(round) == ReviewRoundKind.SpotCheck;

    /// <summary>Trạng thái bản ghi sau khi thêm quyết định của vòng <paramref name="round"/>.</summary>
    /// <param name="previousDecisions">Quyết định của các vòng trước, xếp theo thứ tự vòng.</param>
    /// <param name="selectedForSpotCheck">Chỉ có ý nghĩa ở vòng 1.</param>
    public static RecordingStatus NextStatus(
        int round,
        ReviewDecision decision,
        IReadOnlyList<ReviewDecision> previousDecisions,
        bool selectedForSpotCheck)
    {
        if (previousDecisions.Count != round - 1)
        {
            throw new ArgumentException(
                $"Vòng {round} phải có đúng {round - 1} quyết định trước đó, nhận được {previousDecisions.Count}.",
                nameof(previousDecisions));
        }

        return KindOf(round) switch
        {
            ReviewRoundKind.Primary => selectedForSpotCheck ? RecordingStatus.PendingReview : ToStatus(decision),
            ReviewRoundKind.SpotCheck => decision == previousDecisions[0] ? ToStatus(decision) : RecordingStatus.PendingReview,
            ReviewRoundKind.Adjudication => Adjudicate(decision, previousDecisions),
            _ => throw new UnreachableException()
        };
    }

    private static RecordingStatus Adjudicate(ReviewDecision decision, IReadOnlyList<ReviewDecision> previous)
    {
        // Chỉ có vòng 3 khi hai vòng trước lệch nhau. Tới được đây mà hai vòng lại trùng ý
        // nghĩa là trạng thái bản ghi đã sai từ trước — dừng lại thay vì chốt bừa.
        if (previous[0] == previous[1])
        {
            throw new InvalidOperationException("Hai vòng trước đã trùng ý thì không có vòng phân xử.");
        }

        return ToStatus(decision);
    }

    private static RecordingStatus ToStatus(ReviewDecision decision) =>
        decision == ReviewDecision.Approved ? RecordingStatus.Approved : RecordingStatus.Rejected;
}

public interface IReviewSampler
{
    /// <summary>Có rút bản ghi này ra để kiểm tra lại ở vòng 2 không.</summary>
    bool ShouldSpotCheck(decimal ratio);
}

/// <summary>
/// Rút mẫu ngẫu nhiên theo tỉ lệ cấu hình.
/// Tách thành interface để test thay bằng bản cố định — test không được phụ thuộc may rủi.
/// </summary>
public sealed class RandomReviewSampler : IReviewSampler
{
    public bool ShouldSpotCheck(decimal ratio) => ratio switch
    {
        <= 0m => false,
        >= 1m => true,
        _ => (decimal)Random.Shared.NextDouble() < ratio
    };
}
