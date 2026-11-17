using CodeSwitchLabel.Services.Common;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Services.Audio;

/// <summary>
/// Gọi đường chính trước; hỏng thì gọi đường lui.
///
/// <para>
/// Dùng để đặt dịch vụ trả phí làm đường chính và dịch vụ chạy ngay trên máy chủ làm đường
/// lui. Nhà cung cấp hỏng, hết số dư hay đổi chính sách thì người duyệt chỉ thấy lượt đó
/// chậm hơn chứ không thấy lỗi — và quan trọng hơn, bản ghi không bị kẹt lại ở thế hoà.
/// </para>
///
/// <para>
/// Khai CÙNG một model ở cả hai nơi thì lùi đường không làm đổi phán quyết. Dù vậy
/// <see cref="AsrResult.Model"/> vẫn ghi đúng nơi thực sự làm việc, nên tra lại bảng
/// <c>ai_review</c> là biết lượt nào do ai phiên âm.
/// </para>
/// </summary>
public sealed class FallbackAsrClient(
    IAsrClient primary,
    IAsrClient fallback,
    ILogger<FallbackAsrClient> logger) : IAsrClient
{
    public bool IsConfigured => primary.IsConfigured || fallback.IsConfigured;

    public async Task<AsrResult> TranscribeAsync(
        byte[] audio, string fileName, CancellationToken ct = default)
    {
        if (!primary.IsConfigured)
        {
            // Không cấu hình đường chính thì đi thẳng đường lui, đừng ném lỗi rồi mới lùi:
            // làm vậy mỗi lượt lại ghi một dòng cảnh báo vô nghĩa vào log.
            return await fallback.TranscribeAsync(audio, fileName, ct);
        }

        try
        {
            return await primary.TranscribeAsync(audio, fileName, ct);
        }
        catch (UnprocessableException ex) when (CoTheLuiDuoc(ex.Code) && fallback.IsConfigured)
        {
            logger.LogWarning(
                ex,
                "Đường chính hỏng ({Code}); chuyển sang đường lui cho {FileName}. "
                + "Lượt này sẽ chậm hơn bình thường.",
                ex.Code, fileName);

            return await fallback.TranscribeAsync(audio, fileName, ct);
        }
    }

    /// <summary>
    /// Lỗi nào đáng lùi đường.
    ///
    /// <para>
    /// <c>asr_empty_audio</c> KHÔNG nằm ở đây: file rỗng là lỗi dữ liệu của chính ta, đường
    /// lui cũng chịu y như vậy. Lùi chỉ tốn thêm gần một phút để nhận lại đúng câu trả lời đó.
    /// </para>
    /// </summary>
    private static bool CoTheLuiDuoc(string code) =>
        code is "asr_failed"          // mạng hỏng, 5xx, quá hạn
             or "asr_rate_limited"    // nhà cung cấp chặn tần suất
             or "asr_parse_error"     // trả về thứ không đọc được
             or "asr_not_configured"; // thiếu Endpoint/Model
}
