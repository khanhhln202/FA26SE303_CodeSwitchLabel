using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeSwitchLabel.Services.Audio;

/// <summary>
/// So transcript do ASR đọc được với văn bản người đọc đáng ra phải đọc.
///
/// Viết thành hàm thuần, không phụ thuộc database hay mạng, để test được từng bước
/// riêng — cùng lối với <see cref="RecordingQcEvaluator"/>.
/// </summary>
public static partial class TranscriptComparison
{
    /// <summary>Nhãn ngôn ngữ trong script.cs_content, ví dụ "[vi]Em nên [en]scan [vi]tài liệu".</summary>
    [GeneratedRegex(@"\[(?:vi|en)\]", RegexOptions.IgnoreCase)]
    private static partial Regex LanguageTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>
    /// đ và Đ không phân rã được bằng Unicode NFD như các dấu khác, nên phải thay tay.
    /// Thiếu bước này thì "đọc" và "doc" vẫn tính là hai từ khác nhau sau khi bỏ dấu.
    /// </summary>
    private const string DWithStroke = "đĐ";

    public static string StripLanguageTags(string text) =>
        string.IsNullOrEmpty(text) ? string.Empty : LanguageTag().Replace(text, " ");

    /// <summary>
    /// Chữ thường, bỏ dấu câu, gộp khoảng trắng. Giữ nguyên dấu tiếng Việt —
    /// việc bỏ dấu là bước riêng để còn so được cả hai cách.
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var builder = new StringBuilder(text.Length);

        foreach (var ch in text)
        {
            // Dấu câu, ký hiệu, emoji đều thành khoảng trắng. Chữ số giữ lại vì
            // câu có thể chứa số và người đọc vẫn phải đọc đúng.
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }

        return Whitespace().Replace(builder.ToString(), " ").Trim();
    }

    /// <summary>
    /// Bỏ dấu tiếng Việt. Dùng để tính một WER thứ hai: ASR mất dấu là giới hạn của
    /// mô hình, không phải lỗi người đọc, mà ở đây ta chỉ phán NỘI DUNG.
    /// </summary>
    public static string FoldDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;

            builder.Append(DWithStroke.Contains(ch) ? (char.IsUpper(ch) ? 'D' : 'd') : ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string[] Tokenize(string normalizedText) =>
        string.IsNullOrWhiteSpace(normalizedText)
            ? []
            : normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Word Error Rate = (thay + thiếu + thêm) / số từ của văn bản gốc.
    ///
    /// Có thể vượt 1 khi ASR sinh ra nhiều từ hơn hẳn văn bản gốc — không cắt về 1,
    /// vì "đọc lan sang câu khác" là tín hiệu thật và đáng để lộ ra.
    /// </summary>
    public static decimal WordErrorRate(string[] expected, string[] actual)
    {
        if (expected.Length == 0) return actual.Length == 0 ? 0m : 1m;

        var distance = LevenshteinDistance(expected, actual);

        return Math.Round((decimal)distance / expected.Length, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Khoảng cách Levenshtein mức TỪ, hai hàng thay vì cả ma trận: câu dài nhất ở đây
    /// vài chục từ, nhưng giữ bộ nhớ O(n) thì không phải nghĩ lại khi nhập câu dài hơn.
    /// </summary>
    private static int LevenshteinDistance(string[] source, string[] target)
    {
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];

        for (var j = 0; j <= target.Length; j++) previous[j] = j;

        for (var i = 1; i <= source.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= target.Length; j++)
            {
                var substitution = previous[j - 1] +
                    (string.Equals(source[i - 1], target[j - 1], StringComparison.Ordinal) ? 0 : 1);

                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    /// <summary>
    /// Kết quả so một bản ghi: WER giữ dấu và WER bỏ dấu, kèm hai chuỗi đã chuẩn hoá
    /// để ghi vào bảng ai_review — tra lại được vì sao máy quyết như vậy.
    /// </summary>
    public static TranscriptComparisonResult Compare(string expectedRaw, string actualRaw)
    {
        var expected = Normalize(StripLanguageTags(expectedRaw));
        var actual = Normalize(StripLanguageTags(actualRaw));

        var wer = WordErrorRate(Tokenize(expected), Tokenize(actual));

        var werFolded = WordErrorRate(
            Tokenize(FoldDiacritics(expected)),
            Tokenize(FoldDiacritics(actual)));

        return new TranscriptComparisonResult(expected, actual, wer, werFolded);
    }
}

/// <summary>
/// <paramref name="Wer"/> giữ dấu tiếng Việt, <paramref name="WerFolded"/> đã bỏ dấu.
/// <see cref="Effective"/> là con số dùng để phán.
/// </summary>
public record TranscriptComparisonResult(
    string ExpectedNormalized,
    string ActualNormalized,
    decimal Wer,
    decimal WerFolded)
{
    /// <summary>Lấy con số thấp hơn: không bắt người đọc chịu lỗi mất dấu của ASR.</summary>
    public decimal Effective => Math.Min(Wer, WerFolded);
}
