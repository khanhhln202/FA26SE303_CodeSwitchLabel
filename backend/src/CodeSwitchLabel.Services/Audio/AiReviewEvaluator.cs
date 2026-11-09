using System.Globalization;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Services.Abstractions;

namespace CodeSwitchLabel.Services.Audio;

public enum AiReviewVerdict
{
    /// <summary>Có bằng chứng rõ là đọc đúng nội dung, VÀ cấu hình cho phép AI bỏ phiếu đạt.</summary>
    Approved,

    /// <summary>Có bằng chứng rõ là đọc lệch văn bản.</summary>
    Rejected,

    /// <summary>Không đủ chắc. Ghi bằng chứng, không chèn lượt duyệt nào, để người quyết.</summary>
    Abstained
}

/// <summary>
/// Ngưỡng phán đọc một lần từ system_config, để cả lượt phán dùng chung một ảnh chụp
/// cấu hình — cùng lý do với <see cref="RecordingQcSettings"/>.
/// </summary>
public sealed record AiReviewSettings(
    bool Enabled,
    decimal WerRejectMin,
    decimal WerApproveMax,
    bool AllowApproveVote)
{
    public static AiReviewSettings From(IEnumerable<SystemConfigDto> config)
    {
        var values = config.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);

        decimal Number(string key, decimal fallback) =>
            values.TryGetValue(key, out var raw) &&
            decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;

        bool Flag(string key, bool fallback) =>
            values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value)
                ? value
                : fallback;

        return new(
            Flag(ConfigKeys.AiReviewEnabled, false),
            Number(ConfigKeys.AiReviewWerRejectMin, 0.40m),
            Number(ConfigKeys.AiReviewWerApproveMax, 0.10m),
            Flag(ConfigKeys.AiReviewAllowApproveVote, false));
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
    /// Lý do duy nhất AI được phép nêu. Ba lý do active còn lại — background_noise,
    /// mispronunciation, other — AI không đo được, nên không bao giờ tự chọn.
    /// </summary>
    public const string ContentMismatch = "content_mismatch";

    public static AiReviewDecision Evaluate(decimal effectiveWer, AiReviewSettings settings)
    {
        var wer = Format(effectiveWer);

        // Kiểm ngưỡng TỪ CHỐI trước. Nếu ai đó đặt hai ngưỡng chồng nhau (reject_min
        // thấp hơn approve_max) thì vùng chồng rơi về từ chối — hướng sai an toàn hơn:
        // từ chối oan thì chỉ phải thu lại, duyệt oan thì dữ liệu rác vào dataset.
        if (effectiveWer >= settings.WerRejectMin)
        {
            return new(
                AiReviewVerdict.Rejected,
                [ContentMismatch],
                $"Tỉ lệ sai từ {wer} đạt hoặc vượt ngưỡng {Format(settings.WerRejectMin)}, "
                + "phần đọc lệch so với văn bản.");
        }

        if (effectiveWer <= settings.WerApproveMax)
        {
            if (!settings.AllowApproveVote)
            {
                return new(
                    AiReviewVerdict.Abstained,
                    [],
                    $"Tỉ lệ sai từ {wer} nằm trong ngưỡng đạt {Format(settings.WerApproveMax)}, "
                    + "nhưng cấu hình không cho máy bỏ phiếu đạt: máy chỉ đo được nội dung, "
                    + "chưa phán được tạp âm và phát âm. Nhường người duyệt quyết định.");
            }

            return new(
                AiReviewVerdict.Approved,
                [],
                $"Tỉ lệ sai từ {wer} nằm trong ngưỡng đạt {Format(settings.WerApproveMax)}, "
                + "phần đọc khớp văn bản.");
        }

        return new(
            AiReviewVerdict.Abstained,
            [],
            $"Tỉ lệ sai từ {wer} nằm giữa ngưỡng đạt {Format(settings.WerApproveMax)} "
            + $"và ngưỡng từ chối {Format(settings.WerRejectMin)}. Không đủ chắc để phán, "
            + "nhường người duyệt quyết định.");
    }

    /// <summary>InvariantCulture bắt buộc: máy đặt tiếng Việt dùng dấu phẩy làm dấu thập phân.</summary>
    private static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
