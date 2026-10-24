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
/// Luật duyệt theo lược đồ nhóm chốt: mỗi bản ghi nhận tối đa <c>review.rounds_required</c>
/// lượt duyệt mù độc lập (mặc định 3, đổi được qua <c>system_config</c>) và chốt theo đa số
/// NGAY KHI một bên đã quá bán — những lượt còn lại không lật được kết quả nữa.
///
/// CHỖ CHỐT THẬT SỰ NẰM DƯỚI DATABASE — trigger trg_review_majority. Lớp này chỉ là bản sao
/// để tầng trên biết còn thiếu mấy lượt và để kiểm thử luật, KHÔNG được dùng để ghi trạng thái.
///
/// Trigger đọc cùng một tham số <c>review.rounds_required</c> nên hai bên không thể lệch nhau.
/// </summary>
public static class ReviewRules
{
    /// <summary>Số lượt duyệt mặc định khi chưa có cấu hình.</summary>
    public const int DefaultRoundsRequired = 3;

    public static int NextRound(int existingReviews) => existingReviews + 1;

    public static bool IsComplete(int reviewCount, int roundsRequired) => reviewCount >= roundsRequired;

    /// <summary>
    /// Kết quả theo đa số, viết giống hệt trigger: đủ số lượt thì bên nào quá nửa thì thắng.
    /// Trả null khi chưa đủ lượt — lúc đó bản ghi vẫn nằm chờ.
    /// </summary>
    public static int MajorityOf(int roundsRequired) => (roundsRequired / 2) + 1;

    public static RecordingStatus? Outcome(IReadOnlyList<ReviewDecision> decisions, int roundsRequired)
    {
        if (roundsRequired < 1) throw new ArgumentOutOfRangeException(nameof(roundsRequired));

        var quaBan = MajorityOf(roundsRequired);
        var approved = decisions.Count(d => d == ReviewDecision.Approved);
        var rejected = decisions.Count - approved;

        // Quá bán là chốt, không chờ cho đủ lượt: hai phiếu từ chối trên ba vòng thì lượt
        // thứ ba không đổi được gì, mà bản ghi cứ nằm "chờ duyệt" thì người đọc không biết
        // đường thu lại.
        if (approved >= quaBan) return RecordingStatus.Approved;
        if (rejected >= quaBan) return RecordingStatus.Rejected;

        // Đủ lượt mà chưa bên nào quá bán chỉ xảy ra với số vòng CHẴN (2-2 khi cần 4):
        // không đủ phiếu đạt thì trượt, đúng như luật cũ.
        return decisions.Count >= roundsRequired ? RecordingStatus.Rejected : null;
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
