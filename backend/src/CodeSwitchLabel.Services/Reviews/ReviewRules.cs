using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Reviews;

/// <summary>
/// Vì sao một bản ghi chưa duyệt được. Màn hình danh sách của Reviewer hiện lý do này ngay,
/// thay vì để người ta bấm vào rồi mới nhận lỗi.
/// </summary>
public enum ReviewBlocker
{
    /// <summary>Duyệt được ngay.</summary>
    None,

    /// <summary>Bản ghi do chính người đang xem thu.</summary>
    OwnRecording,

    /// <summary>Người đang xem đã duyệt bản này rồi; mỗi người chỉ được một lượt.</summary>
    AlreadyReviewedByMe,

    /// <summary>Bản ghi không còn ở trạng thái chờ duyệt.</summary>
    NotPendingReview,

    /// <summary>Đã đủ số lượt duyệt cần có, đang chờ database chốt.</summary>
    RoundsFull
}

/// <summary>
/// Luật duyệt theo lược đồ nhóm chốt: mỗi bản ghi cần ĐỦ BA LƯỢT duyệt mù độc lập,
/// rồi chốt theo đa số.
///
/// CHỖ CHỐT THẬT SỰ NẰM DƯỚI DATABASE — trigger trg_review_majority. Lớp này chỉ là bản sao
/// để tầng trên biết còn thiếu mấy lượt và để kiểm thử luật, KHÔNG được dùng để ghi trạng thái.
///
/// Lưu ý: trigger ghim cứng con số 3. Đổi tham số review.rounds_required mà không sửa trigger
/// thì hai bên lệch nhau — ReviewService ghi log cảnh báo khi phát hiện.
/// </summary>
public static class ReviewRules
{
    /// <summary>Số lượt duyệt mà trigger của database đang chờ.</summary>
    public const int RoundsRequiredInDatabase = 3;

    public static int NextRound(int existingReviews) => existingReviews + 1;

    public static bool IsComplete(int reviewCount, int roundsRequired) => reviewCount >= roundsRequired;

    /// <summary>
    /// Kết quả theo đa số, viết giống hệt trigger: đủ số lượt thì bên nào quá nửa thì thắng.
    /// Trả null khi chưa đủ lượt — lúc đó bản ghi vẫn nằm chờ.
    /// </summary>
    public static RecordingStatus? Outcome(IReadOnlyList<ReviewDecision> decisions, int roundsRequired)
    {
        if (roundsRequired < 1) throw new ArgumentOutOfRangeException(nameof(roundsRequired));
        if (decisions.Count < roundsRequired) return null;

        var approved = decisions.Count(d => d == ReviewDecision.Approved);

        return approved >= (roundsRequired / 2) + 1
            ? RecordingStatus.Approved
            : RecordingStatus.Rejected;
    }

    /// <summary>
    /// Đúng những điều kiện mà GET next đang lọc, nhưng viết thành một hàm thuần để dùng lại được
    /// cho danh sách và cho lúc Reviewer mở thẳng một bản ghi.
    ///
    /// Thứ tự kiểm có chủ ý: bản của chính mình là lý do gốc; đã duyệt rồi thì nói vậy kể cả khi
    /// bản ghi đã chốt xong, vì màn hình cần đánh dấu "mình làm rồi".
    /// </summary>
    public static ReviewBlocker BlockerFor(
        bool isOwnRecording,
        bool reviewedByMe,
        RecordingStatus status,
        int reviewCount,
        int roundsRequired)
    {
        if (isOwnRecording) return ReviewBlocker.OwnRecording;
        if (reviewedByMe) return ReviewBlocker.AlreadyReviewedByMe;
        if (status != RecordingStatus.PendingReview) return ReviewBlocker.NotPendingReview;
        if (IsComplete(reviewCount, roundsRequired)) return ReviewBlocker.RoundsFull;

        return ReviewBlocker.None;
    }
}
