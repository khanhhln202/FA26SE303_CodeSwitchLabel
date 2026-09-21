using System.Text;

namespace CodeSwitchLabel.Repositories.Persistence;

/// <param name="IsEnglish">Đoạn này nằm trong nhãn [en] hay [vi].</param>
public readonly record struct LanguageSpan(bool IsEnglish, string Text);

/// <summary>
/// Xử lý câu có nhãn ngôn ngữ theo đúng định dạng input_text.json của giảng viên:
///
///   [vi]Em nên [en]scan [vi]tài liệu này rồi gửi qua [en]email [vi]cho tôi.
///
/// Nhãn có hiệu lực cho tới nhãn kế tiếp. Nhờ giữ nguyên nhãn trong cột cs_content mà
/// số từ tiếng Anh là ĐẾM CHÍNH XÁC, không còn phải đoán theo dấu tiếng Việt như bản cũ —
/// cách đoán đó từng đếm "qua" và "nay" thành từ tiếng Anh.
/// </summary>
public static class CodeSwitchText
{
    public const string ViTag = "[vi]";
    public const string EnTag = "[en]";

    /// <summary>Tách câu thành các đoạn theo nhãn. Ném lỗi nếu chuỗi không đúng định dạng.</summary>
    /// <exception cref="FormatException">Thiếu nhãn mở đầu, hoặc gặp nhãn lạ.</exception>
    public static IReadOnlyList<LanguageSpan> Parse(string tagged)
    {
        var text = (tagged ?? string.Empty).Trim();

        if (!text.StartsWith(ViTag, StringComparison.Ordinal) &&
            !text.StartsWith(EnTag, StringComparison.Ordinal))
        {
            throw new FormatException("Câu phải bắt đầu bằng nhãn [vi] hoặc [en].");
        }

        var spans = new List<LanguageSpan>();
        var index = 0;

        while (index < text.Length)
        {
            var isEnglish = text.AsSpan(index).StartsWith(EnTag);
            index += ViTag.Length;   // [vi] và [en] cùng dài 4 ký tự

            var next = NextTagIndex(text, index);
            var body = text[index..next];

            if (body.Length > 0) spans.Add(new LanguageSpan(isEnglish, body));

            index = next;
        }

        if (spans.Count == 0) throw new FormatException("Câu rỗng, không có nội dung nào sau nhãn.");

        return spans;
    }

    /// <summary>Bỏ nhãn, còn lại câu chữ thường để hiển thị cho người đọc và để so trùng.</summary>
    public static string Strip(string tagged)
    {
        var builder = new StringBuilder(tagged.Length);

        foreach (var span in Parse(tagged)) builder.Append(span.Text);

        return CollapseSpaces(builder.ToString());
    }

    /// <summary>Tổng số từ của câu, tính trên bản đã bỏ nhãn.</summary>
    public static int CountWords(string tagged) => Tokenize(Strip(tagged)).Length;

    /// <summary>Số từ nằm trong các đoạn [en]. Đây là con số đi vào chữ số đầu của script_id.</summary>
    public static int CountEnglishWords(string tagged) =>
        Parse(tagged).Where(s => s.IsEnglish).Sum(s => Tokenize(s.Text).Length);

    /// <summary>Cắt câu thành từ: bỏ dấu câu, gộp khoảng trắng, hạ chữ thường.</summary>
    public static string[] Tokenize(string plain)
    {
        var builder = new StringBuilder(plain.Length);

        foreach (var ch in plain)
        {
            if (char.IsWhiteSpace(ch)) { builder.Append(' '); continue; }
            if (char.IsPunctuation(ch) || char.IsSymbol(ch)) continue;

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static int NextTagIndex(string text, int from)
    {
        for (var i = from; i <= text.Length - ViTag.Length; i++)
        {
            if (text[i] != '[') continue;

            if (text.AsSpan(i).StartsWith(ViTag) || text.AsSpan(i).StartsWith(EnTag)) return i;

            // Dấu ngoặc vuông không phải nhãn ngôn ngữ là dấu hiệu file nhập sai định dạng.
            if (text.AsSpan(i).StartsWith("[")) throw new FormatException(
                $"Chỉ chấp nhận nhãn [vi] và [en], gặp \"{text[i..Math.Min(i + 6, text.Length)]}\".");
        }

        return text.Length;
    }

    private static string CollapseSpaces(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSpace = true;

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) { builder.Append(' '); lastWasSpace = true; }
                continue;
            }

            builder.Append(ch);
            lastWasSpace = false;
        }

        return builder.ToString().TrimEnd();
    }
}
