using CodeSwitchLabel.Repositories.Entities;

namespace CodeSwitchLabel.Services.Reviews;

/// <summary>Cách hiển thị một lượt duyệt bản ghi — dùng chung cho mọi màn hình lịch sử.</summary>
public static class ReviewDisplay
{
    /// <summary>
    /// Lý do hiển thị của một lượt duyệt: bình luận là chính; lượt từ chối không kèm bình luận
    /// thì ghép mô tả các mã lý do từ chối, để dòng lịch sử luôn có lý do để hiện.
    ///
    /// Cần <c>RejectionReasons</c> (kèm <c>Reason</c>) đã nạp sẵn; thiếu thì trả về bình luận
    /// hoặc null chứ không tự truy vấn thêm.
    /// </summary>
    public static string? ReasonOf(Review review)
    {
        if (!string.IsNullOrWhiteSpace(review.Comment)) return review.Comment;

        var reasons = review.RejectionReasons
            .Select(rr => rr.Reason.Description ?? rr.Reason.ReasonCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        return reasons.Count > 0 ? string.Join(", ", reasons) : null;
    }
}
