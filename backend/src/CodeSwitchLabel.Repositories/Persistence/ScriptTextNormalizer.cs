using System.Globalization;
using System.Text;

namespace CodeSwitchLabel.Repositories.Persistence;

/// <summary>
/// Đếm từ và ước lượng số từ tiếng Anh, phục vụ hai cột word_count và en_word_count
/// trong bảng script.
/// </summary>
public static class ScriptTextNormalizer
{
    /// <summary>Chữ thường, bỏ dấu câu, gộp khoảng trắng. Dùng để đếm từ và so trùng.</summary>
    public static string Normalize(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;

        var builder = new StringBuilder(content.Length);
        var lastWasSpace = true;

        foreach (var ch in content)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) { builder.Append(' '); lastWasSpace = true; }
                continue;
            }

            if (char.IsPunctuation(ch) || char.IsSymbol(ch)) continue;

            builder.Append(char.ToLowerInvariant(ch));
            lastWasSpace = false;
        }

        return builder.ToString().TrimEnd();
    }

    public static string[] Tokenize(string content) =>
        Normalize(content).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static int CountWords(string content) => Tokenize(content).Length;

    /// <summary>
    /// ƯỚC LƯỢNG số từ tiếng Anh trong câu.
    ///
    /// Cách làm: một từ được coi là tiếng Anh nếu nó không mang dấu tiếng Việt
    /// và không nằm trong danh sách từ tiếng Việt không dấu hay gặp.
    ///
    /// GIỚI HẠN ĐÃ BIẾT: đây là phỏng đoán, không phải nhận dạng ngôn ngữ thật.
    /// Nó nhận nhầm những từ tiếng Việt không dấu chưa có trong danh sách
    /// (ví dụ "cham", "hoc"), và cũng không biết từ tiếng Anh nằm ở VỊ TRÍ nào.
    ///
    /// Vì ERD chỉ có cột đếm mà không có bảng lưu vị trí, kết quả này chỉ đủ
    /// để thống kê và lọc, không đủ để tô màu phần tiếng Anh hay xuất nhãn
    /// theo từ vào manifest dataset. API cho phép người nhập ghi đè con số này.
    /// </summary>
    public static int CountEnglishWords(string content)
    {
        return Tokenize(content).Count(IsLikelyEnglish);
    }

    private static bool IsLikelyEnglish(string token)
    {
        if (token.Length < 2) return false;

        // Có dấu tiếng Việt thì chắc chắn không phải tiếng Anh.
        if (HasVietnameseDiacritics(token)) return false;

        // Chỉ nhận từ gồm chữ cái ASCII.
        if (!token.All(c => c is >= 'a' and <= 'z')) return false;

        return !VietnameseWordsWithoutDiacritics.Contains(token);
    }

    private static bool HasVietnameseDiacritics(string token)
    {
        // Tách ký tự có dấu thành chữ gốc + dấu, rồi xem có dấu nào không.
        var decomposed = token.Normalize(NormalizationForm.FormD);

        return decomposed.Any(c =>
            CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            || token.Contains('đ');
    }

    /// <summary>
    /// Từ tiếng Việt viết không dấu hay gặp. Danh sách ngắn có chủ ý —
    /// đủ để tránh những nhầm lẫn phổ biến nhất mà không giả vờ là từ điển đầy đủ.
    /// </summary>
    private static readonly HashSet<string> VietnameseWordsWithoutDiacritics =
    [
        "anh", "em", "toi", "ban", "minh", "chi", "ong", "ba", "co", "chu",
        "la", "co", "khong", "cho", "cua", "va", "voi", "khi", "thi", "ma",
        "den", "di", "ve", "ra", "vao", "len", "xuong", "tren", "duoi", "trong",
        "nay", "do", "kia", "gi", "sao", "nao", "dau", "bao", "nhieu", "rat",
        "cung", "van", "con", "da", "se", "dang", "vua", "moi", "nua", "roi",
        "mot", "hai", "ba", "bon", "nam", "sau", "bay", "tam", "chin", "muoi",
        "ngay", "thang", "nam", "tuan", "gio", "phut", "sang", "chieu", "toi", "dem",
        "lam", "an", "uong", "noi", "nghe", "nhin", "biet", "hieu", "nho", "quen",
        "tan", "nha", "nhe", "a", "oi", "u", "ha", "he", "hihi",
        "xin", "giup", "dum", "thay", "ban", "nhom", "lop", "bai", "cau", "tu"
    ];
}
