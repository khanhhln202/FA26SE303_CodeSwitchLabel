namespace CodeSwitchLabel.Services.Options;

/// <summary>
/// Endpoint OpenAI-compatible dùng cho bước chuyển âm thanh thành chữ (/audio/transcriptions),
/// phục vụ tính năng máy duyệt bản ghi.
///
/// Để trống Endpoint/Model thì API vẫn khởi động bình thường; riêng endpoint máy duyệt trả
/// `asr_not_configured` thay vì làm sập ứng dụng — cùng lối với <see cref="LlmOptions"/>.
/// </summary>
public class AsrOptions
{
    public const string SectionName = "Asr";

    /// <summary>Base URL, ví dụ <c>https://api.openai.com/v1</c> hoặc <c>http://localhost:8000/v1</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Không bắt buộc — endpoint nội bộ thường không cần khoá.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Ví dụ <c>whisper-1</c>.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// ĐỂ TRỐNG là đúng cho dự án này. Khai "vi" thì mô hình cố nghe mọi thứ thành tiếng Việt
    /// và phiên âm các từ tiếng Anh thành chữ Việt gần âm, làm WER vọt lên oan cho người đọc.
    /// Câu trộn Việt–Anh cần để mô hình tự nhận.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Chuyển âm thanh chậm hơn gọi chat nhiều, nên mặc định cao hơn LlmOptions.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}
