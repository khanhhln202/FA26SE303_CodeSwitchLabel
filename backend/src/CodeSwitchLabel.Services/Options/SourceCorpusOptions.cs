namespace CodeSwitchLabel.Services.Options;

/// <summary>
/// Cấu hình luồng YouTube → ASR → corpus. Để trống đường dẫn thì API vẫn chạy;
/// chỉ job tải video báo lỗi cấu hình, không làm sập các module khác.
/// </summary>
public class SourceCorpusOptions
{
    public const string SectionName = "SourceCorpus";

    /// <summary>Để "yt-dlp" thì tìm trong PATH; image Docker đã cài sẵn.</summary>
    public string YtDlpPath { get; set; } = "yt-dlp";

    /// <summary>
    /// Runtime JavaScript cho yt-dlp, ví dụ "deno". YouTube cần runtime này để giải mã
    /// đầy đủ; để trống thì yt-dlp chạy ở mức hỗ trợ hạn chế.
    /// </summary>
    public string JsRuntimes { get; set; } = "deno";

    /// <summary>
    /// File cookie Netscape cho yt-dlp. YouTube chặn IP máy chủ bằng "Sign in to confirm
    /// you're not a bot"; có cookie của tài khoản phụ thì tải được. Để trống hoặc file
    /// không tồn tại thì bỏ qua.
    /// </summary>
    public string CookiesPath { get; set; } = "";

    /// <summary>Tải video dài có thể mất hàng chục phút; đây là trần cho cả tiến trình tải.</summary>
    public int ProcessTimeoutSeconds { get; set; } = 1800;

    /// <summary>
    /// Trần riêng cho một lượt phiên âm. Video dài chạy máy CPU có thể lâu hơn
    /// Asr:TimeoutSeconds của máy duyệt bản ghi ngắn.
    /// </summary>
    public int AsrTimeoutSeconds { get; set; } = 900;

    /// <summary>Số lần thử trước khi chuyển job sang Failed.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Thời gian một Reviewer giữ job trước khi job quay lại hàng đợi.</summary>
    public int ClaimMinutes { get; set; } = 60;

    /// <summary>Nhịp quét hàng đợi khi không có việc.</summary>
    public int PollSeconds { get; set; } = 10;
}
