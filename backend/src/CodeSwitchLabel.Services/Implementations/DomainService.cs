using System.Text.RegularExpressions;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodeSwitchLabel.Services.Implementations;

/// <summary>
/// Chủ đề là dữ liệu, không phải ENUM. Bảng chỉ có tối đa 9 dòng (một chữ số
/// trong script_id), nên truy vấn trực tiếp mỗi request là rẻ — không cần cache
/// tránh lỗi đồng bộ hóa khi nhiều request chạy đồng thời.
/// </summary>
public class DomainService(CodeSwitchLabelDbContext db) : IDomainService
{
    /// <summary>Chữ số chưa dùng cho cột id_digit (CHAR(1), CHECK '1'..'9').</summary>
    private static readonly string[] FreeDigits = ["4", "5", "6", "7", "8", "9"];

    public async Task<IReadOnlyList<TopicDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        return await db.Domains.AsNoTracking()
            .Where(d => includeInactive || d.IsActive)
            .OrderBy(d => d.IdDigit)
            .Select(d => new TopicDto(d.Code, d.Name, d.IsActive))
            .ToListAsync(ct);
    }

    public async Task<TopicDto> CreateAsync(string name, CancellationToken ct = default)
    {
        var cleanName = DomainCodeGenerator.CleanName(name);
        var all = await GetAllAsync(ct);

        if (all.Any(d => d.IsActive && string.Equals(d.Name, cleanName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException(
                "domain_name_exists", @$"Chủ đề ""{cleanName}"" đã tồn tại.");
        }

        var digit = FreeDigits.FirstOrDefault(d => all.All(x => x.IdDigit != d))
            ?? throw new ConflictException(
                "domain_limit_reached",
                "Đã đủ 9 chủ đề (giới hạn của chữ số chủ đề trong mã câu); không tạo thêm được.");

        var code = DomainCodeGenerator.UniqueCode(
            DomainCodeGenerator.MakeCode(cleanName), all.Select(d => d.Code));

        var domain = new Domain
        {
            Code = code,
            Name = cleanName,
            IdDigit = digit,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Domains.Add(domain);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, out var constraint))
        {
            if (constraint is not null
                && constraint.Contains("id_digit", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "domain_limit_reached",
                    "Đã đủ 9 chủ đề (giới hạn của chữ số chủ đề trong mã câu); không tạo thêm được.");
            }

            if (constraint is not null
                && constraint.Contains("uq_domain_active_name", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "domain_name_exists", @$"Chủ đề ""{cleanName}"" đã tồn tại.");
            }

            if (constraint is not null
                && (constraint.Equals("domain_pkey", StringComparison.OrdinalIgnoreCase)
                    || constraint.Contains("pkey", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException(
                    "domain_code_exists", @$"Mã chủ đề ""{code}"" đã tồn tại.");
            }

            throw;
        }

        return ToDto(domain);
    }

    public async Task<TopicDto> RenameAsync(string code, string name, CancellationToken ct = default)
    {
        var cleanName = DomainCodeGenerator.CleanName(name);
        var all = await GetAllAsync(ct);

        var domain = all.FirstOrDefault(d =>
                d.IsActive && string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("domain_not_found", @$"Không tìm thấy chủ đề ""{code}"".");

        if (all.Any(d => d.IsActive && d.Code != domain.Code
                && string.Equals(d.Name, cleanName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException(
                "domain_name_exists", @$"Chủ đề ""{cleanName}"" đã tồn tại.");
        }

        domain.Name = cleanName;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, out var constraint))
        {
            if (constraint is not null
                && constraint.Contains("uq_domain_active_name", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "domain_name_exists", @$"Chủ đề ""{cleanName}"" đã tồn tại.");
            }

            throw;
        }

        return ToDto(domain);
    }

    public async Task DeleteAsync(string code, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);

        var domain = all.FirstOrDefault(d =>
                string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("domain_not_found", @$"Không tìm thấy chủ đề ""{code}"".");

        if (!domain.IsActive)
        {
            // Đã xóa mềm rồi — idempotent, không báo lỗi.
            return;
        }

        // Phải còn ít nhất một chủ đề đang hoạt động.
        if (all.Count(d => d.IsActive) <= 1)
        {
            throw new ConflictException(
                "domain_last_active",
                "Không xóa chủ đề cuối cùng — hệ thống phải còn ít nhất một chủ đề.");
        }

        // Không xóa chủ đề đang được tham chiếu bởi dữ liệu hiện có — ngăn
        // các đợt/câu/nhiệm vụ đang dùng bị "khóa" không sửa được sau này.
        var inUse = await db.Scripts.AnyAsync(s => s.Domain == code, ct)
            || await db.Waves.AnyAsync(w => w.Domain == code, ct)
            || await db.WorkTasks.AnyAsync(t => t.Domain == code, ct)
            || await db.UserDomains.AnyAsync(u => u.Domain == code, ct);

        if (inUse)
        {
            throw new ConflictException(
                "domain_in_use",
                "Không xóa chủ đề vì đang được dùng bởi câu/đợt/nhiệm vụ hoặc phân quyền người dùng.");
        }

        domain.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Nhận code hoặc tên (bỏ dấu, không phân biệt hoa thường) và trả code chuẩn.
    /// <paramref name="allowInactive"/> chỉ dùng cho các đường dẫn cập nhật dữ liệu cũ
    /// (ví dụ sửa đợt/câu mà chủ đề đã bị xóa mềm) — mọi đường dẫn tạo mới để mặc định false.
    /// Chủ đề đã xóa mềm bị từ chối khi <paramref name="allowInactive"/> là false.
    /// </summary>
    public async Task<string> ResolveActiveAsync(string codeOrName, CancellationToken ct = default,
        bool allowInactive = false)
    {
        var value = (codeOrName ?? string.Empty).Trim();
        var all = await GetAllAsync(ct);
        var key = DomainCodeGenerator.MatchKey(value);

        var domain = all.FirstOrDefault(d => DomainCodeGenerator.MatchKey(d.Code) == key)
            ?? all.FirstOrDefault(d => DomainCodeGenerator.MatchKey(d.Name) == key);

        if (domain is null)
        {
            var activeNames = all.Where(d => d.IsActive).Select(d => d.Name).Take(5).ToList();
            var listed = string.Join(", ", activeNames);
            var remaining = all.Count(d => d.IsActive) - activeNames.Count;
            if (remaining > 0)
            {
                listed = listed + $" ... và {remaining} khác";
            }

            throw new UnprocessableException(
                "unknown_domain", @$"Chủ đề ""{value}"" không tồn tại. Chủ đề hiện có: {listed}.");
        }

        if (!domain.IsActive && !allowInactive)
        {
            throw new UnprocessableException(
                "domain_inactive", @$"Chủ đề ""{domain.Name}"" đã bị xóa; chọn chủ đề khác.");
        }

        return domain.Code;
    }

    private Task<List<Domain>> GetAllAsync(CancellationToken ct) =>
        db.Domains.ToListAsync(ct);

    private static TopicDto ToDto(Domain d) => new(d.Code, d.Name, d.IsActive);

    /// <summary>
    /// Kiểm tra <see cref="DbUpdateException"/> có do vi phạm ràng buộc duy nhất của PostgreSQL
    /// (SqlState 23505) không, và tách ra tên constraint nếu có.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex, out string? constraintName)
    {
        constraintName = null;
        Exception? current = ex;
        while (current is not null)
        {
            if (current is NpgsqlException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                constraintName = ExtractConstraintName(pg.Message ?? string.Empty)
                    ?? ExtractConstraintName(current.Message ?? string.Empty);
                return true;
            }
            current = current.InnerException;
        }

        return false;
    }

    private static string? ExtractConstraintName(string message)
    {
        var match = Regex.Match(message, @"unique constraint ""([^""]+)""", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}
