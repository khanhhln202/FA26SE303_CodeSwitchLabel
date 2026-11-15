using System.Globalization;
using System.Text;
using CodeSwitchLabel.Services.Common;

namespace CodeSwitchLabel.Services.Common;

/// <summary>
/// Các hàm sinh mã/chuẩn hóa chủ đề thuần túy, không phụ thuộc DB — dễ kiểm thử đơn lẻ.
/// Tách khỏi <see cref="Implementations.DomainService"/> để các hàm tính toán này có thể
/// được cover bằng unit test mà không cần khởi động DbContext.
/// </summary>
internal static class DomainCodeGenerator
{
    /// <summary>Chuẩn hóa tên chủ đề: trim, cấm trống, dài 2..100 ký tự.</summary>
    public static string CleanName(string name)
    {
        var clean = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clean))
        {
            throw new UnprocessableException(
                "invalid_domain_name", "Tên chủ đề không được để trống.");
        }

        if (clean.Length is < 2 or > 100)
        {
            throw new UnprocessableException(
                "invalid_domain_name", "Tên chủ đề phải từ 2 đến 100 ký tự.");
        }

        return clean;
    }

    /// <summary>"Y tế" → "YTe"; "IT/Technology" → "ItTechnology".</summary>
    public static string MakeCode(string name)
    {
        var plain = Unaccent(name);
        var sb = new StringBuilder();
        var upper = true;

        foreach (var c in plain)
        {
            if (!char.IsLetterOrDigit(c))
            {
                upper = true;
                continue;
            }

            sb.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }

        return sb.Length == 0 ? "Domain" : sb.ToString();
    }

    /// <summary>Bỏ dấu tiếng Việt, không phân biệt hoa thường.</summary>
    public static string Unaccent(string value) =>
        new string(value.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray())
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .ToLowerInvariant();

    /// <summary>Chỉ giữ chữ và số: "IT/Technology" và "IT Technology" cùng một khóa.</summary>
    public static string MatchKey(string value) =>
        new string(Unaccent(value).Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Nếu code đã tồn tại thì gán suffix 2, 3, ... cho tới khi duy nhất.</summary>
    public static string UniqueCode(string code, IEnumerable<string> existingCodes)
    {
        if (!existingCodes.Any(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase)))
        {
            return code;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = code + suffix;
            if (!existingCodes.Any(c => string.Equals(c, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }
}
