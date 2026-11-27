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

        // Bỏ trống thì lấy bản ĐANG HIỆU LỰC, không phải chuỗi "v1" cứng: khi phát hành v2
        // thì mặc định cũng phải đi theo, nếu không client cũ sẽ ký mãi vào bản đã lui.
        var version = string.IsNullOrWhiteSpace(request.ConsentVersion)
            ? (await ActiveDocumentAsync(ct)).Version
            : request.ConsentVersion.Trim();

        // Phiên bản phải có nội dung thật. Trước đây đây là chuỗi tự do nên client gửi
        // "v999" hay "abc" cũng được ghi nhận, và hệ thống khẳng định một người đã đồng ý
        // với văn bản không tồn tại. Database cũng chặn (khoá ngoại V39); kiểm ở đây để
        // trả lỗi đọc được thay vì để lộ lỗi ràng buộc ra ngoài.
        if (!await db.ConsentDocuments.AnyAsync(d => d.Version == version, ct))
        {
            throw new UnprocessableException(
                "consent_version_unknown",
                $"Không có bản cam kết phiên bản \"{version}\". Lấy bản đang hiệu lực ở GET /api/consent.");
        }

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

    public async Task<ConsentDocumentDto> GetActiveDocumentAsync(CancellationToken ct = default) =>
        ToDto(await ActiveDocumentAsync(ct));

    public async Task<ConsentDocumentDto?> GetDocumentAsync(
        string version, CancellationToken ct = default)
    {
        var d = await db.ConsentDocuments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Version == version, ct);

        return d is null ? null : ToDto(d);
    }

    public async Task<IReadOnlyList<ConsentDocumentDto>> ListDocumentsAsync(
        CancellationToken ct = default) =>
        [.. (await db.ConsentDocuments.AsNoTracking()
                .OrderByDescending(d => d.EffectiveFrom)
                .ThenByDescending(d => d.CreatedAt)
                .ToListAsync(ct))
            .Select(ToDto)];

    public async Task<ConsentDocumentDto> PublishDocumentAsync(
        PublishConsentDocumentRequest request, long createdBy, CancellationToken ct = default)
    {
        var version = request.Version.Trim();

        if (await db.ConsentDocuments.AnyAsync(d => d.Version == version, ct))
        {
            throw new ConflictException(
                "consent_version_exists",
                $"Phiên bản \"{version}\" đã tồn tại. Bản đã phát hành không được ghi đè — "
                + "đặt mã khác, vì có thể đã có người đồng ý bản này.");
        }

        // Hạ bản cũ TRƯỚC khi thêm bản mới: chỉ mục một phần dưới database chỉ cho đúng
        // một dòng is_active, nên làm ngược thứ tự là vỡ ràng buộc giữa chừng.
        foreach (var cu in await db.ConsentDocuments.Where(d => d.IsActive).ToListAsync(ct))
        {
            cu.IsActive = false;
        }

        var moi = new ConsentDocument
        {
            Version = version,
            Title = request.Title.Trim(),
            Body = request.Body,
            EffectiveFrom = request.EffectiveFrom ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
            IsActive = true,
            CreatedBy = createdBy,
            CreatedAt = clock.GetUtcNow()
        };

        db.ConsentDocuments.Add(moi);
        await db.SaveChangesAsync(ct);

        return ToDto(moi);
    }

    private async Task<ConsentDocument> ActiveDocumentAsync(CancellationToken ct) =>
        await db.ConsentDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.IsActive, ct)
        ?? throw new NotFoundException(
            "consent_document_missing",
            "Chưa có bản cam kết nào đang hiệu lực. Admin phát hành một bản ở "
            + "POST /api/admin/consent-documents trước khi mời người dùng đồng ý.");

    private static ConsentDocumentDto ToDto(ConsentDocument d) =>
        new(d.Version, d.Title, d.Body, d.EffectiveFrom, d.IsActive);
}
