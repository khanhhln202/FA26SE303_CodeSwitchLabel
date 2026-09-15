using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CodeSwitchLabel.Services.Implementations;

public class AuthService(
    IUserRepository users,
    IPasswordHasher<AppUser> hasher,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim(), ct);

        // Cố ý trả CÙNG một thông báo cho cả hai ca sai email và sai mật khẩu.
        // Nói rõ "email không tồn tại" là tiết lộ tài khoản nào có thật trong hệ thống.
        if (user is null)
        {
            throw new BadRequestException("invalid_credentials", "Email hoặc mật khẩu không đúng.");
        }

        var verify = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verify == PasswordVerificationResult.Failed)
        {
            throw new BadRequestException("invalid_credentials", "Email hoặc mật khẩu không đúng.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new ForbiddenException("account_disabled", "Tài khoản đã bị vô hiệu hoá.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwt.ExpiryMinutes);
        return new LoginResponse(BuildToken(user, expiresAt), expiresAt, ToDto(user));
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(long userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct)
                   ?? throw new NotFoundException("user_not_found", "Không tìm thấy tài khoản.");

        return ToDto(user);
    }

    private static CurrentUserDto ToDto(AppUser user) =>
        new(user.UserId, user.Email, user.FullName,
            user.Role.RoleName.ToString(), user.SpeakerProfile is not null);

    private string BuildToken(AppUser user, DateTimeOffset expiresAt)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.FullName),

            // Một vai duy nhất, đúng như ERD. Nhờ vậy [Authorize(Roles = "...")] vẫn chạy bình thường.
            new(ClaimTypes.Role, user.Role.RoleName.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class ScriptAssignmentService(
    IScriptRepository repository,
    ISystemConfigService config,
    TimeProvider clock) : IScriptAssignmentService
{
    public async Task<NextScriptDto?> GetNextAsync(
        long speakerId, long? taskId, CancellationToken ct = default)
    {
        var script = await repository.GetNextForSpeakerAsync(speakerId, taskId, ct);
        if (script is null) return null;

        var guidance = new RecordingGuidanceDto(
            await config.GetDecimalAsync(ConfigKeys.RecordingMinDurationSec, 1m, ct),
            await config.GetDecimalAsync(ConfigKeys.RecordingMaxDurationSec, 30m, ct));

        return new NextScriptDto(
            script.ScriptId, script.Content, script.Domain,
            script.WordCount, script.EnWordCount, guidance);
    }

    public async Task SkipAsync(
        long scriptId, long speakerId, long? taskId, CancellationToken ct = default)
    {
        _ = await repository.GetAsync(scriptId, ct) ?? throw NotFoundException.Script(scriptId);

        // Bỏ qua hai lần cùng một script là vô nghĩa. Database cũng chặn bằng
        // ràng buộc duy nhất, nhưng kiểm ở đây để trả lỗi 409 tử tế thay vì lỗi ràng buộc thô.
        if (await repository.HasSkippedAsync(scriptId, speakerId, ct))
        {
            throw new ConflictException(
                "already_skipped", $"Bạn đã bỏ qua script #{scriptId} rồi.");
        }

        repository.AddSkip(new ScriptSkip
        {
            ScriptId = scriptId,
            SpeakerId = speakerId,
            TaskId = taskId,
            SkippedAt = clock.GetUtcNow()
        });

        await repository.SaveChangesAsync(ct);
    }
}

public class SystemConfigService(ISystemConfigRepository repository) : ISystemConfigService
{
    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var config = await repository.GetAsync(key, ct);
        if (config is null) return fallback;

        return int.TryParse(config.ConfigValue, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
    {
        var config = await repository.GetAsync(key, ct);
        if (config is null) return fallback;

        // InvariantCulture bắt buộc: máy đặt tiếng Việt dùng dấu phẩy làm dấu thập phân,
        // đọc "0.35" sẽ ra 35 nếu không ghim culture.
        return decimal.TryParse(config.ConfigValue, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    public async Task<IReadOnlyList<SystemConfigDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await repository.GetAllAsync(ct);

        return [.. items.Select(c => new SystemConfigDto(
            c.ConfigKey, c.ConfigValue, c.ValueType.ToString(), c.Description, c.UpdatedAt))];
    }

    public async Task UpdateAsync(string key, string value, long updatedById, CancellationToken ct = default)
    {
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
