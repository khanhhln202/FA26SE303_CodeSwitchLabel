namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Ba giá trị hợp lệ của ai_review.verdict, khớp đúng ràng buộc ck_ai_review_verdict
/// trong migration V23. Để ở tầng Repositories vì đây là hợp đồng với database —
/// tầng Services có enum riêng và tự ánh xạ sang mấy chuỗi này.
/// </summary>
public static class AiReviewVerdicts
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Abstained = "abstained";
}

/// <summary>
/// Tài khoản người duyệt máy, do migration V23 tạo. Tra theo email chứ không ghi cứng
/// user_id: cột app_user.user_id là identity nên số trên máy dev, máy CI và máy chủ
/// không giống nhau.
/// </summary>
public static class AiReviewer
{
    public const string Email = "ai-reviewer@codeswitchlabel.local";
}

/// <summary>
/// Bằng chứng của một lượt máy phán: transcript ASR đọc được, văn bản đáng ra phải đọc,
/// và tỉ lệ sai từ.
///
/// CỐ Ý không nhét vào review.comment: comment là chỗ người viết cho người đọc, còn đây
/// là dữ liệu máy sinh ra và cần truy vấn được (thống kê độ chính xác của máy, tra lại
/// vì sao một bản bị loại).
///
/// <see cref="ReviewId"/> null nghĩa là máy BỎ QUYỀN — có bằng chứng nhưng không có lượt
/// duyệt nào. Ràng buộc ck_ai_review_vote_has_review dưới database bắt chặt chiều này:
/// verdict có bỏ phiếu thì bắt buộc trỏ tới một dòng review, và ngược lại.
/// </summary>
public class AiReview
{
    public long AiReviewId { get; set; }

    public string RecordingId { get; set; } = string.Empty;

    /// <summary>Null khi bỏ quyền.</summary>
    public long? ReviewId { get; set; }

    /// <summary>Tên mô hình ASR đã dùng, để so lại khi đổi mô hình.</summary>
    public string Model { get; set; } = string.Empty;

    public string Transcript { get; set; } = string.Empty;

    /// <summary>Văn bản gốc sau khi bỏ nhãn và chuẩn hoá, chính là chuỗi đã đem so.</summary>
    public string ExpectedText { get; set; } = string.Empty;

    /// <summary>Tỉ lệ sai từ khi giữ dấu tiếng Việt.</summary>
    public decimal Wer { get; set; }

    /// <summary>Tỉ lệ sai từ sau khi bỏ dấu. Con số nhỏ hơn giữa hai cái là cái đem phán.</summary>
    public decimal WerFolded { get; set; }

    /// <summary>Một trong <see cref="AiReviewVerdicts"/>.</summary>
    public string Verdict { get; set; } = AiReviewVerdicts.Abstained;

    public DateTimeOffset AnalyzedAt { get; set; }

    /// <summary>jsonb: ngưỡng đã dùng và lời giải thích, để tra lại quyết định cũ.</summary>
    public string? Details { get; set; }

    public Recording Recording { get; set; } = null!;
    public Review? Review { get; set; }
}
