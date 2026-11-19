using System.Globalization;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Services.Abstractions;

namespace CodeSwitchLabel.Services.Audio;

/// <summary>
/// Chỉ hai giá trị. Máy chỉ vào khi hai Reviewer người lệch nhau, tức nó là người PHÁ THẾ,
/// nên nó luôn phải chọn một bên — không có "bỏ quyền". Bỏ quyền lúc đó để lại bản ghi
/// 1–1 không ai quyết, tức thế vẫn chưa được phá.
/// </summary>
public enum AiReviewVerdict
{
    Approved,
    Rejected
}

/// <summary>
/// Ngưỡng phán đọc một lần từ system_config, để cả lượt phán dùng chung một ảnh chụp
/// cấu hình — cùng lý do với <see cref="RecordingQcSettings"/>.
/// </summary>
public sealed record AiReviewSettings(bool Enabled, decimal WerDecisionThreshold)
{
    /// <summary>
    /// Sai một từ là từ chối — luật đội frontend chốt ngày 18/11/2026.
    ///
    /// <para>
    /// 0.0001 chứ KHÔNG phải 0. <see cref="AiReviewEvaluator.Evaluate"/> so bằng
    /// <c>&gt;=</c>, nên đặt 0 sẽ làm <c>0 &gt;= 0</c> đúng và từ chối cả bản ghi đọc khớp
    /// hoàn hảo — tức từ chối tất cả. Giá trị này nằm lọt giữa "không sai từ nào"
    /// (WER = 0) và "sai ít nhất một từ" (WER = 1/số từ).
    /// </para>
    ///
    /// <para>
    /// Ngưỡng cũ 0.40 KHÔNG phải chọn bừa, và cái giá của việc bỏ nó đã được đo: trên 17
    /// lượt máy đã chấm, 8 bản đạt nằm trong 0.00–0.30 và 9 bản không đạt nằm trong
    /// 0.63–1.13, giữa hai cụm không có bản ghi nào. Luật mới loại 15 trong 17 bản đó, kể
    /// cả <c>r_cs_121001014</c> mà người đọc và máy nghe đều đúng từng chữ — toàn bộ WER
    /// 0.30 của nó đến từ việc "12 giờ 19" được viết thành "12h19".
    /// </para>
    ///
    /// <para>
    /// Gốc vấn đề nằm ở cái THƯỚC chứ không ở cái NGƯỠNG: WER đang gộp "người đọc sai",
    /// "máy nghe sai" và "chỉ khác cách viết" vào cùng một con số. Chuẩn hoá cách viết số
    /// và dấu thanh trước khi so sẽ tách được ba thứ đó. Chưa làm.
    /// </para>
    /// </summary>
    public const decimal DefaultThreshold = 0.0001m;

    public static AiReviewSettings From(IEnumerable<SystemConfigDto> config)
    {
        var values = config.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);

        var enabled =
            values.TryGetValue(ConfigKeys.AiReviewEnabled, out var rawEnabled) &&
            bool.TryParse(rawEnabled, out var parsedEnabled) && parsedEnabled;

        var threshold =
            values.TryGetValue(ConfigKeys.AiReviewWerDecisionThreshold, out var rawThreshold) &&
            decimal.TryParse(rawThreshold, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : DefaultThreshold;

        return new(enabled, threshold);
    }
}

public record AiReviewDecision(
    AiReviewVerdict Verdict,
    IReadOnlyList<string> RejectionReasonCodes,
    string Explanation);

/// <summary>
/// Biến một con số WER thành quyết định. Hàm thuần, không đụng database hay mạng,
/// nên test được từng nhánh ngưỡng mà không cần dựng gì.
/// </summary>
public static class AiReviewEvaluator
{
    /// <summary>
    /// Lý do duy nhất máy được phép nêu. Ba lý do active còn lại — background_noise,
    /// mispronunciation, other — máy không đo được, nên không bao giờ tự chọn.
    /// </summary>
    public const string ContentMismatch = "content_mismatch";

    public static AiReviewDecision Evaluate(decimal effectiveWer, AiReviewSettings settings)
    {
        var wer = Format(effectiveWer);
        var cut = Format(settings.WerDecisionThreshold);

        if (effectiveWer >= settings.WerDecisionThreshold)
        {
            return new(
                AiReviewVerdict.Rejected,
                [ContentMismatch],
                $"Tỉ lệ sai từ {wer} đạt hoặc vượt ranh giới {cut}, phần đọc lệch so với văn bản. "
                + "Máy vào làm phiếu thứ ba vì hai người duyệt trước cho kết quả khác nhau.");
        }

        return new(
            AiReviewVerdict.Approved,
            [],
            $"Tỉ lệ sai từ {wer} dưới ranh giới {cut}, phần đọc khớp văn bản. "
            + "Máy vào làm phiếu thứ ba vì hai người duyệt trước cho kết quả khác nhau.");
    }

    /// <summary>InvariantCulture bắt buộc: máy đặt tiếng Việt dùng dấu phẩy làm dấu thập phân.</summary>
    private static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
