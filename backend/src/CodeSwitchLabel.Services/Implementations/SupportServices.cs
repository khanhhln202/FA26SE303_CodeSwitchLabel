using System.Globalization;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Options;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CodeSwitchLabel.Services.Implementations;

public class AuthService(
    IUserRepository users,
    IPasswordHasher hasher,
    IAccessTokenIssuer tokens,
    IObjectStorage storage,
    TimeProvider clock,
    IOptions<GoogleAuthOptions> googleOptions) : IAuthService
{
    private static string? _dummyHash;
    // shortcut: the first unknown-email login hashes once; prewarm if cold-start timing matters.

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim(), ct);

        // Luôn chạy đúng một lần Verify, dù email có tồn tại hay không,
        // để thời gian phản hồi không lộ email nào có thật.
        var hash = user?.PasswordHash ?? (_dummyHash ??= hasher.Hash(Guid.NewGuid().ToString("N")));
        var passwordOk = hasher.Verify(request.Password, hash);

        // Cố ý trả CÙNG một thông báo cho cả hai ca sai email và sai mật khẩu.
        if (user is null || !passwordOk)
        {
            throw new BadRequestException("invalid_credentials", "Email hoặc mật khẩu không đúng.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new ForbiddenException("account_disabled", "Tài khoản đã bị vô hiệu hoá.");
        }

        return user.ToLoginResponse(tokens.Issue(user));
    }

    public async Task<LoginResponse> GoogleLoginAsync(
        GoogleLoginRequest request, CancellationToken ct = default)
    {
        var clientId = googleOptions.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ServiceUnavailableException("google_auth_not_configured", "Đăng nhập Google chưa được cấu hình.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.Credential,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (InvalidJwtException)
        {
            throw new BadRequestException("invalid_google_credential", "Thông tin đăng nhập Google không hợp lệ hoặc đã hết hạn.");
        }

        if (string.IsNullOrWhiteSpace(payload.Email) || payload.EmailVerified != true ||
            string.IsNullOrWhiteSpace(payload.Subject))
            throw new BadRequestException("unverified_google_account", "Tài khoản Google chưa xác thực email.");

        var user = await users.GetByEmailAsync(payload.Email, ct);
        if (user is null)
        {
            if (!request.ConsentAccepted)
                throw new UnprocessableException("consent_required", "Cần đồng ý điều khoản trước khi tạo tài khoản.");

            return await RegisterSpeakerAsync(new RegisterSpeakerRequest
            {
                Email = payload.Email,
                FullName = string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name,
                Password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                ConsentAccepted = true,
                ConsentVersion = request.ConsentVersion
            }, ct);
        }

        if (user.Status != UserStatus.Active)
            throw new ForbiddenException("account_disabled", "Tài khoản đã bị vô hiệu hoá.");

        return user.ToLoginResponse(tokens.Issue(user));
    }

    public async Task<LoginResponse> RegisterSpeakerAsync(
        RegisterSpeakerRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await users.EmailExistsAsync(email, ct))
        {
            throw new ConflictException("email_taken", "Email đã được đăng ký.");
        }

        // Phiên bản cam kết phải có NỘI DUNG THẬT trong consent_document.
        //
        // Trước đây chỗ này ghi cứng "v1" khi client bỏ trống, và nhận nguyên xi bất kỳ chuỗi
        // nào client khai — nên trên máy chủ thật dòng cam kết duy nhất đang mang phiên bản
        // "string", tức giá trị mặc định của ô nhập trong Swagger. Hệ thống khẳng định người
        // đó đã đồng ý một văn bản không tồn tại.
        var consentVersion = request.ConsentAccepted
            ? await ResolveConsentVersionAsync(request.ConsentVersion, ct)
            : null;

        var profile = request.SpeakerProfile;
        var user = new AppUser
        {
            Role = await users.GetRoleAsync(RoleName.Speaker, ct),
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = Clean(request.Phone),
            PasswordHash = hasher.Hash(request.Password),
            Status = UserStatus.Active,
            CreatedAt = clock.GetUtcNow(),
            SpeakerProfile = new SpeakerProfile
            {
                BirthYear = profile?.BirthYear,
                BirthPlace = Clean(profile?.BirthPlace),
                Province = Clean(profile?.Province),
                Region = profile?.Region,
                EnglishLevel = profile?.EnglishLevel,
                Occupation = profile?.Occupation,
                Gender = profile?.Gender,
                Major = Clean(profile?.Major)
            },
            Consent = consentVersion is null
                ? null
                : new UserConsent
                {
                    Version = consentVersion,
                    AcceptedAt = clock.GetUtcNow()
                }
        };

        users.Add(user);

        try
        {
            await users.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when
            (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("email_taken", "Email đã được đăng ký.");
        }

        return user.ToLoginResponse(tokens.Issue(user));
    }

    public async Task AcceptConsentAsync(
        long userId, string version, Stream? document, string? documentName, string? contentType,
        CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct)
                   ?? throw new NotFoundException("user_not_found", $"Không tìm thấy tài khoản #{userId}.");

        string? key = null;
        string? storedName = null;

        if (document is not null && !string.IsNullOrWhiteSpace(documentName))
        {
            var extension = Path.GetExtension(documentName);
            var tempPath = Path.Combine(Path.GetTempPath(), $"consent-{Guid.NewGuid():N}{extension}");

            try
            {
                await using (var file = File.Create(tempPath))
                {
                    await document.CopyToAsync(file, ct);
                }

                key = $"consent/{user.UserId}/{Guid.NewGuid():N}{extension}";
                storedName = Path.GetFileName(documentName);
                await storage.UploadAsync(
                    key, tempPath, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, ct);
            }
            finally
            {
                try { File.Delete(tempPath); } catch (IOException) { }
            }
        }

        var phienBan = await ResolveConsentVersionAsync(version, ct);

        await users.UpsertConsentAsync(userId, phienBan, clock.GetUtcNow(), key, storedName, ct);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Mã phiên bản cam kết sẽ được ghi lại, sau khi chắc chắn nó có NỘI DUNG THẬT.
    ///
    /// <para>
    /// Dùng chung cho lúc đăng ký và lúc nộp bản đã ký, để hai đường không thể lệch luật:
    /// ghi nhận được một phiên bản ở đường này mà đường kia từ chối thì bảng
    /// <c>consent_document</c> mất tác dụng.
    /// </para>
    ///
    /// <para>
    /// Bỏ trống thì lấy bản ĐANG HIỆU LỰC chứ không ghi cứng "v1": phát hành v2 xong thì mặc
    /// định phải đi theo, nếu không client cũ ký mãi vào bản đã lui.
    /// </para>
    /// </summary>
    private async Task<string> ResolveConsentVersionAsync(string? requested, CancellationToken ct)
    {
        var version = string.IsNullOrWhiteSpace(requested)
            ? await users.GetActiveConsentVersionAsync(ct)
              ?? throw new UnprocessableException(
                  "consent_document_missing",
                  "Chưa có bản cam kết nào đang hiệu lực. Admin phát hành một bản ở "
                  + "POST /api/admin/consent-documents trước khi mời người dùng đồng ý.")
            : requested.Trim();

        // Database cũng chặn (khoá ngoại V39), nhưng lúc đó người dùng nhận một lỗi ràng buộc
        // thay vì câu nói rõ phải làm gì.
        if (!await users.ConsentVersionExistsAsync(version, ct))
        {
            throw new UnprocessableException(
                "consent_version_unknown",
                $"Không có bản cam kết phiên bản \"{version}\". "
                + "Lấy bản đang hiệu lực ở GET /api/consent.");
        }

        return version;
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(long userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct)
                   ?? throw new NotFoundException("user_not_found", "Không tìm thấy tài khoản.");

        return user.ToCurrentUser();
    }
}

public class SystemConfigService(ISystemConfigRepository repository) : ISystemConfigService
{
    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default) =>
        int.TryParse(await GetRawAsync(key, ct), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var value) ? value : fallback;

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
    {
        var raw = (await GetRawAsync(key, ct))?.Trim();

        // Lược đồ lưu 'true'/'false', nhưng người sửa tay hay gõ 1/0 nên nhận cả hai.
        return raw switch
        {
            "1" => true,
            "0" => false,
            _ => bool.TryParse(raw, out var value) ? value : fallback
        };
    }

    // InvariantCulture bắt buộc: máy đặt tiếng Việt dùng dấu phẩy làm dấu thập phân.
    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default) =>
        decimal.TryParse(await GetRawAsync(key, ct), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private async Task<string?> GetRawAsync(string key, CancellationToken ct) =>
        (await repository.GetAsync(key, ct))?.ConfigValue;

    public async Task<IReadOnlyList<SystemConfigDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await repository.GetAllAsync(ct);

        return [.. items.Select(c => new SystemConfigDto(
            c.ConfigKey, c.ConfigValue, c.ValueType.ToString(), c.Description, c.UpdatedAt))];
    }

    public async Task<SystemConfigDto> GetAsync(string key, CancellationToken ct = default)
    {
        var c = await repository.GetAsync(key, ct)
            ?? throw new NotFoundException("config_not_found", $"Không có tham số cấu hình \"{key}\".");

        return new SystemConfigDto(
            c.ConfigKey, c.ConfigValue, c.ValueType.ToString(), c.Description, c.UpdatedAt);
    }

    public async Task UpdateAsync(string key, string value, long updatedById, CancellationToken ct = default)
    {
        if (key == ConfigKeys.ScriptMaxEnglishWords &&
            (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxEnglishWords) ||
             maxEnglishWords is < 1 or > 9))
        {
            throw new BadRequestException(
                "invalid_config_value",
                "Số từ tiếng Anh tối đa phải là số nguyên từ 1 đến 9.");
        }

        if (!await repository.UpdateValueAsync(key, value, updatedById, ct))
        {
            throw new NotFoundException("config_not_found", $"Không có tham số cấu hình \"{key}\".");
        }
    }
}

public class ReasonService(IReasonRepository repository) : IReasonService
{
    public async Task<IReadOnlyList<ReasonDto>> GetRejectionReasonsAsync(
        bool activeOnly, CancellationToken ct = default)
    {
        var items = await repository.GetRejectionReasonsAsync(activeOnly, ct);

        return [.. items.Select(r => new ReasonDto(
            r.ReasonId, r.ReasonCode, r.Category.ToString(), r.Description, r.IsActive))];
    }

    public async Task<IReadOnlyList<ReasonDto>> GetScriptErrorReasonsAsync(
        bool activeOnly, CancellationToken ct = default)
    {
        var items = await repository.GetScriptErrorReasonsAsync(activeOnly, ct);

        return [.. items.Select(r => new ReasonDto(
            r.ReasonId, r.ReasonCode, null, r.Description, r.IsActive))];
    }
}

/// <summary>
/// Số liệu quản trị. Mọi con số đọc thẳng từ view của lược đồ, không tính lại trong C# —
/// dashboard và báo cáo SQL vì thế không bao giờ lệch nhau.
/// </summary>
public class StatisticsService(IStatisticsRepository repository, IWaveRepository waves)
    : IStatisticsService
{
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var s = await repository.GetDashboardAsync(ct);

        return new DashboardDto(
            s.TotalWaves, s.OpenWaves, s.ActiveWaves,
            s.TotalScripts, s.ValidatedScripts, s.PendingScripts,
            s.TotalRecordings, s.ApprovedRecordings, s.RejectedRecordings,
            s.ApprovedDurationSec,
            Math.Round((double)s.ApprovedDurationSec / 3600, 2),
            s.Speakers, s.Reviewers, s.ReleasedDatasets);
    }

    public async Task<IReadOnlyList<SpeakerQualityDto>> GetSpeakerQualityAsync(CancellationToken ct = default) =>
        [.. (await repository.GetSpeakerPerformanceAsync(ct)).Select(s => new SpeakerQualityDto(
            s.UserId, s.FullName, s.Recordings, s.Approved, s.Rejected, s.ApprovalRatePct))];

    public async Task<IReadOnlyList<ReviewerQualityDto>> GetReviewerQualityAsync(CancellationToken ct = default) =>
        [.. (await repository.GetReviewerPerformanceAsync(ct)).Select(r => new ReviewerQualityDto(
            r.UserId, r.FullName, r.ReviewsDone, r.Approvals, r.Rejections))];

    public async Task<IReadOnlyList<RejectionStatDto>> GetRejectionStatsAsync(CancellationToken ct = default) =>
        [.. (await repository.GetRejectionStatsAsync(ct)).Select(r => new RejectionStatDto(
            r.ReasonCode, r.Category.ToString(), r.TimesUsed))];

    public async Task<IReadOnlyList<WaveProgressDto>> GetWaveProgressAsync(
        CancellationToken ct = default) =>
        [.. (await waves.GetProgressAsync(null, ct)).Select(p => new WaveProgressDto(
            p.WaveId, p.WaveName, p.Domain, p.WaveTargetQty, p.StartDate, p.EndDate,
            p.WaveStatus, p.AssignedTo, p.AllocatedTaskQty, p.RemainingTaskQty,
            p.TaskCount, p.CompletedTaskCount))];
}
