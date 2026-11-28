using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>Gán nhãn [vi]/[en] cho một cặp câu thô bằng luật, dùng LLM khi luật không đủ.</summary>
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
    /// Gán nhãn tự động: chạy luật trước; chỉ gọi LLM (nếu đã cấu hình) khi luật để trống
    /// nghĩa tiếng Việt, có từ ASCII có thể là tiếng Việt nhưng câu đối chiếu không xác nhận,
    /// hoặc báo hai câu lệch nghĩa. LLM lỗi hoặc trả kết quả không đọc
    /// được thì giữ kết quả luật.
    /// </summary>
    Task<LabeledPair> AutoLabelAsync(string csRaw, string viRaw, CancellationToken ct = default);
}

/// <summary>Câu đã gán nhãn và alignment tương ứng, sẵn sàng đưa vào luồng import hiện có.</summary>
/// <param name="SemanticWarning">Cảnh báo lệch nghĩa: LLM trả khi hai câu lệch nghĩa; luật trả khi từ [vi] của câu trộn không khớp câu thuần Việt. Null khi khớp.</param>
public record LabeledPair(string CsTagged, List<AlignmentItem> Alignment, string? SemanticWarning = null, bool HasUncertainLanguage = false);
