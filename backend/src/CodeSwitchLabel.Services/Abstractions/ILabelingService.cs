using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>Gán nhãn [vi]/[en] cho một cặp câu thô bằng LLM OpenAI-compatible.</summary>
public interface ILabelingService
{
    /// <summary>False khi thiếu Llm:Endpoint hoặc Llm:Model.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Trả về câu đã chèn nhãn và alignment cho từng từ tiếng Anh.
    /// Ném <see cref="Common.UnprocessableException"/> mã `llm_failed`/`llm_parse_error`
    /// khi gọi hoặc đọc kết quả thất bại.
    /// </summary>
    Task<LabeledPair> LabelAsync(string csRaw, string viRaw, CancellationToken ct = default);

    /// <summary>
    /// Gán nhãn tự động: dùng LLM nếu đã cấu hình, ngược lại dùng luật.
    /// LLM lỗi gọi hoặc trả kết quả không đọc được cũng lùi về luật.
    /// </summary>
    Task<LabeledPair> AutoLabelAsync(string csRaw, string viRaw, CancellationToken ct = default);
}

/// <summary>Câu đã gán nhãn và alignment tương ứng, sẵn sàng đưa vào luồng import hiện có.</summary>
/// <param name="SemanticWarning">LLM báo hai câu lệch nghĩa; null khi khớp hoặc khi gán nhãn bằng luật.</param>
public record LabeledPair(string CsTagged, List<AlignmentItem> Alignment, string? SemanticWarning = null);
