using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Reviews;

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
}
