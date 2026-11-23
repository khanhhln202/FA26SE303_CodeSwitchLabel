using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Services.Implementations;

/// <summary>Quản lý đồng ý cam kết sử dụng dữ liệu sinh trắc âm thanh (bảng user_consent).</summary>
public class ConsentService(
    IUserRepository users,
    CodeSwitchLabelDbContext db,
    TimeProvider clock) : IConsentService
{
    public async Task<ConsentDto> RecordConsentAsync(
        long userId, RecordConsentRequest request, CancellationToken ct = default)
    {
        if (!request.ConsentAccepted)
        {
            throw new UnprocessableException(
                "consent_not_accepted", "Bạn phải đồng ý với nội dung cam kết trước khi tiếp tục.");
        }

        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("user_not_found", $"Không tìm thấy tài khoản #{userId}.");

        var version = string.IsNullOrWhiteSpace(request.ConsentVersion) ? "v1" : request.ConsentVersion.Trim();
        var now = clock.GetUtcNow();

        var existing = await db.UserConsents.FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (existing is null)
        {
            db.UserConsents.Add(new UserConsent
            {
                UserId = userId,
                Version = version,
                AcceptedAt = now,
                DocumentKey = null,
                DocumentName = null
            });
        }
        else
        {
            existing.Version = version;
            existing.AcceptedAt = now;
        }

        await db.SaveChangesAsync(ct);

        var saved = await db.UserConsents.FirstAsync(c => c.UserId == userId, ct);
        return new ConsentDto(saved.UserId, saved.Version, saved.AcceptedAt, saved.DocumentKey, saved.DocumentName);
    }

    public async Task<ConsentDto?> GetConsentAsync(long userId, CancellationToken ct = default)
    {
        var c = await db.UserConsents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        return c is null ? null : new ConsentDto(c.UserId, c.Version, c.AcceptedAt, c.DocumentKey, c.DocumentName);
    }

    private static NotFoundException NotFound(string code, string message) =>
        new(code, message);
}
