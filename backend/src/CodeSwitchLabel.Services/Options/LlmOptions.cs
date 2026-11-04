namespace CodeSwitchLabel.Services.Options;

/// <summary>
/// Endpoint OpenAI-compatible dùng cho bước gán nhãn [vi]/[en] khi nhập file .txt.
/// Để trống Endpoint/Model thì API vẫn khởi động bình thường; riêng import .txt sẽ báo
/// `llm_not_configured` thay vì làm sập ứng dụng.
/// </summary>
public class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Base URL, ví dụ <c>https://api.openai.com/v1</c> hoặc <c>http://localhost:11434/v1</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Không bắt buộc — endpoint nội bộ thường không cần khoá.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;
}
