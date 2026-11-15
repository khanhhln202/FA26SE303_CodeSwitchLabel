using System.Globalization;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Services.Abstractions;

namespace CodeSwitchLabel.Services.Audio;

/// <summary>
/// Cấu hình của chế độ máy duyệt tự động. Hàm thuần, không đụng database hay mạng, nên
/// test được từng nhánh mà không cần dựng gì — cùng lối với <see cref="AiReviewSettings"/>.
/// </summary>
public sealed record AiReviewAutoSettings(bool Enabled, TimeSpan Interval, int BatchSize)
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Chặn dưới 10 giây: mỗi bản ghi tốn khoảng 50 giây nhận dạng, nên để ngắn hơn cũng
    /// không nhanh hơn được, chỉ làm log đầy thêm và quét database vô ích.
    /// </summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(10);

    /// <summary>Chặn trên một giờ để một giá trị gõ nhầm không làm tính năng chết lặng.</summary>
    public static readonly TimeSpan MaxInterval = TimeSpan.FromHours(1);

    public const int DefaultBatchSize = 3;
    public const int MaxBatchSize = 20;

    public static AiReviewAutoSettings From(IEnumerable<SystemConfigDto> config)
    {
        var values = config.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);

        var enabled =
            values.TryGetValue(ConfigKeys.AiReviewAutoEnabled, out var rawEnabled) &&
            bool.TryParse(rawEnabled, out var parsedEnabled) && parsedEnabled;

        var interval =
            values.TryGetValue(ConfigKeys.AiReviewAutoIntervalSeconds, out var rawInterval) &&
            int.TryParse(rawInterval, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? TimeSpan.FromSeconds(seconds)
                : DefaultInterval;

        var batch =
            values.TryGetValue(ConfigKeys.AiReviewAutoBatchSize, out var rawBatch) &&
            int.TryParse(rawBatch, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBatch)
                ? parsedBatch
                : DefaultBatchSize;

        // Kẹp chứ không ném lỗi: một ô cấu hình gõ sai không đáng làm chết tiến trình nền.
        return new(
            enabled,
            interval < MinInterval ? MinInterval : interval > MaxInterval ? MaxInterval : interval,
            Math.Clamp(batch, 1, MaxBatchSize));
    }
}

/// <summary>
/// Đếm số lần máy thử một bản ghi mà hỏng, để một bản hỏng vĩnh viễn không chiếm chỗ mãi.
///
/// <para>
/// Không có nó thì một bản ghi mà ASR không bao giờ đọc được — file cụt, định dạng lạ —
/// sẽ ngốn khoảng 50 giây CPU mỗi lượt quét, mãi mãi, và đẩy những bản lành phía sau phải
/// chờ. Quá số lần cho phép thì bỏ qua và để người xử lý tay; bản ghi vẫn nằm trong
/// danh sách chờ nên không ai mất dấu nó.
/// </para>
///
/// <para>
/// Bộ đếm nằm trong bộ nhớ, mất khi API khởi động lại. Đó là đánh đổi có ý: lưu xuống
/// database thì phải thêm cột và migration cho một thứ chỉ là van an toàn, mà khởi động
/// lại thì thử lại vài lần nữa cũng không hại gì.
/// </para>
/// </summary>
public sealed class AiReviewRetryBudget(int maxAttempts = AiReviewRetryBudget.DefaultMaxAttempts)
{
    public const int DefaultMaxAttempts = 3;

    private readonly Dictionary<string, int> _failures = new(StringComparer.Ordinal);

    public bool CanTry(string recordingId) => Attempts(recordingId) < maxAttempts;

    public int Attempts(string recordingId) =>
        _failures.TryGetValue(recordingId, out var n) ? n : 0;

    public int RecordFailure(string recordingId) =>
        _failures[recordingId] = Attempts(recordingId) + 1;

    /// <summary>
    /// Xoá bộ đếm khi thành công: lần hỏng trước có thể chỉ là ASR trục trặc nhất thời,
    /// không phải bản ghi hỏng.
    /// </summary>
    public void RecordSuccess(string recordingId) => _failures.Remove(recordingId);

    /// <summary>Các bản ghi đã bị bỏ qua hẳn — để ghi log và báo cho người xử lý tay.</summary>
    public IReadOnlyCollection<string> GivenUp =>
        _failures.Where(kv => kv.Value >= maxAttempts).Select(kv => kv.Key).ToList();
}
