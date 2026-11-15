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
    /// 0.40 cố ý cao. ASR gặp câu trộn Việt–Anh đã sai sẵn một phần, nên ranh giới thấp
    /// sẽ loại oan người đọc đúng.
    /// </summary>
    public const decimal DefaultThreshold = 0.40m;

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
