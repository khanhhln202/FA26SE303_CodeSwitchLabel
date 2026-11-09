namespace CodeSwitchLabel.Repositories.Entities;

/// <summary>
/// Hai giá trị hợp lệ của ai_review.verdict. V24 dựng bảng với ba giá trị; V25 siết lại
/// còn hai khi luật đổi sang "máy là phiếu thứ ba phá thế". Để ở tầng Repositories vì đây
/// là hợp đồng với database — tầng Services có enum riêng và tự ánh xạ sang hai chuỗi này.
///
/// Không có "abstained": máy chỉ vào khi hai Reviewer người lệch nhau, tức nó là người
/// phá thế, nên luôn phải chọn một bên.
/// </summary>
public static class AiReviewVerdicts
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

/// <summary>
/// Tài khoản người duyệt máy, do migration V24 tạo. Tra theo email chứ không ghi cứng
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
/// <see cref="ReviewId"/> không nullable: máy chỉ vào để phá thế nên mỗi dòng bằng chứng
/// luôn gắn với một lượt duyệt thật. Lược đồ chặn chiều này bằng NOT NULL.
/// </summary>
public class AiReview
{
    public long AiReviewId { get; set; }

    public string RecordingId { get; set; } = string.Empty;

    public long ReviewId { get; set; }

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
    public string Verdict { get; set; } = AiReviewVerdicts.Rejected;

    public DateTimeOffset AnalyzedAt { get; set; }

    /// <summary>jsonb: ngưỡng đã dùng và lời giải thích, để tra lại quyết định cũ.</summary>
    public string? Details { get; set; }

    public Recording Recording { get; set; } = null!;
    public Review Review { get; set; } = null!;
}
