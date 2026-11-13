using System.Globalization;
using System.Text;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Services.Implementations;

/// <summary>
/// Chủ đề là dữ liệu, không phải ENUM. Bảng chỉ có tối đa 9 dòng (một chữ số
/// trong script_id), nên nạp cả bảng một lần cho mỗi request là rẻ và tránh
/// truy vấn lặp khi import hàng loạt.
/// </summary>
public class DomainService(CodeSwitchLabelDbContext db) : IDomainService
{
    private static readonly string[] FreeDigits = ["4", "5", "6", "7", "8", "9"];

    private List<Domain>? cache;

    public async Task<IReadOnlyList<TopicDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var all = await AllAsync(ct);
        return all
            .Where(d => includeInactive || d.IsActive)
            .OrderBy(d => d.IdDigit)
            .Select(ToDto)
            .ToList();
    }

    public async Task<TopicDto> CreateAsync(string name, CancellationToken ct = default)
    {
        var cleanName = CleanName(name);
        var all = await AllAsync(ct);

        if (all.Any(d => d.IsActive && string.Equals(d.Name, cleanName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException(
                "domain_name_exists", $"Chủ đề \"{cleanName}\" đã tồn tại.");
        }

        var digit = FreeDigits.FirstOrDefault(d => all.All(x => x.IdDigit != d))
            ?? throw new ConflictException(
                "domain_limit_reached",
                "Đã đủ 9 chủ đề (giới hạn của chữ số chủ đề trong mã câu); không tạo thêm được.");

        var code = UniqueCode(MakeCode(cleanName), all);

        var domain = new Domain
        {
            Code = code,
            Name = cleanName,
            IdDigit = digit,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Domains.Add(domain);
        await db.SaveChangesAsync(ct);
        cache = null;

        return ToDto(domain);
    }

    public async Task<TopicDto> RenameAsync(string code, string name, CancellationToken ct = default)
    {
        var cleanName = CleanName(name);
        var all = await AllAsync(ct);

        var domain = all.FirstOrDefault(d =>
                d.IsActive && string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("domain_not_found", $"Không tìm thấy chủ đề \"{code}\".");

        if (all.Any(d => d.IsActive && d.Code != domain.Code
                && string.Equals(d.Name, cleanName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException(
                "domain_name_exists", $"Chủ đề \"{cleanName}\" đã tồn tại.");
        }

        domain.Name = cleanName;
        await db.SaveChangesAsync(ct);
        cache = null;

        return ToDto(domain);
    }

    public async Task DeleteAsync(string code, CancellationToken ct = default)
    {
        var all = await AllAsync(ct);

        var domain = all.FirstOrDefault(d =>
                d.IsActive && string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("domain_not_found", $"Không tìm thấy chủ đề \"{code}\".");

        if (all.Count(d => d.IsActive) <= 1)
        {
            throw new ConflictException(
                "domain_last_active", "Không xóa chủ đề cuối cùng — hệ thống phải còn ít nhất một chủ đề.");
        }

        domain.IsActive = false;
        await db.SaveChangesAsync(ct);
        cache = null;
    }

    public async Task<string> ResolveActiveAsync(string codeOrName, CancellationToken ct = default)
    {
        var value = (codeOrName ?? string.Empty).Trim();
        var all = await AllAsync(ct);
        var key = MatchKey(value);

        var domain = all.FirstOrDefault(d => MatchKey(d.Code) == key) ??
                     all.FirstOrDefault(d => MatchKey(d.Name) == key);

        if (domain is null)
        {
            var names = string.Join(", ", all.Where(d => d.IsActive).Select(d => d.Name));
            throw new UnprocessableException(
                "unknown_domain",
                $"Chủ đề \"{value}\" không tồn tại. Chủ đề hiện có: {names}.");
        }

        if (!domain.IsActive)
        {
            throw new UnprocessableException(
                "domain_inactive",
                $"Chủ đề \"{domain.Name}\" đã bị xóa; chọn chủ đề khác.");
        }

        return domain.Code;
    }

    private async Task<List<Domain>> AllAsync(CancellationToken ct) =>
        cache ??= await db.Domains.ToListAsync(ct);

    private static TopicDto ToDto(Domain d) => new(d.Code, d.Name, d.IsActive);

    private static string CleanName(string name)
    {
        var clean = (name ?? string.Empty).Trim();
        if (clean.Length is < 2 or > 100)
        {
            throw new UnprocessableException(
                "invalid_domain_name", "Tên chủ đề phải từ 2 đến 100 ký tự.");
        }

        return clean;
    }

    /// <summary>"Y tế" → "YTe"; "IT/Technology" → "ItTechnology" (dùng cho import cũ).</summary>
    private static string MakeCode(string name)
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

    private static string UniqueCode(string code, IReadOnlyList<Domain> all)
    {
        if (all.All(d => !string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            return code;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = code + suffix;
            if (all.All(d => !string.Equals(d.Code, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }

    /// <summary>Bỏ dấu tiếng Việt, không phân biệt hoa thường.</summary>
    private static string Unaccent(string value) =>
        new string(value.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray())
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .ToLowerInvariant();

    /// <summary>Chỉ giữ chữ và số: "IT/Technology" và "IT Technology" cùng một khóa.</summary>
    private static string MatchKey(string value) =>
        new string(Unaccent(value).Where(char.IsLetterOrDigit).ToArray());
}
