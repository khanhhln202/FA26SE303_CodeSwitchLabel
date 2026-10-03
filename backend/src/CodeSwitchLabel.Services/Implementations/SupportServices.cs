using System.Globalization;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Implementations;

public class AuthService(
    IUserRepository users,
    IPasswordHasher hasher,
    IAccessTokenIssuer tokens) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim(), ct);

        // Cố ý trả CÙNG một thông báo cho cả hai ca sai email và sai mật khẩu:
        // nói rõ "email không tồn tại" là tiết lộ tài khoản nào có thật trong hệ thống.
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new BadRequestException("invalid_credentials", "Email hoặc mật khẩu không đúng.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new ForbiddenException("account_disabled", "Tài khoản đã bị vô hiệu hoá.");
        }

        return user.ToLoginResponse(tokens.Issue(user));
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
    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var config = await repository.GetAsync(key, ct);
        if (config is null) return fallback;

        return int.TryParse(config.ConfigValue, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
    {
        var config = await repository.GetAsync(key, ct);
        if (config is null) return fallback;

        var raw = config.ConfigValue.Trim();

        // Lược đồ lưu 'true'/'false', nhưng người sửa tay hay gõ 1/0 nên nhận cả hai.
        return raw switch
        {
            "1" => true,
            "0" => false,
            _ => bool.TryParse(raw, out var value) ? value : fallback
        };
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
    {
        var config = await repository.GetAsync(key, ct);
        if (config is null) return fallback;

        // InvariantCulture bắt buộc: máy đặt tiếng Việt dùng dấu phẩy làm dấu thập phân.
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

/// <summary>
/// Số liệu quản trị. Mọi con số đọc thẳng từ view của lược đồ, không tính lại trong C# —
/// dashboard và báo cáo SQL vì thế không bao giờ lệch nhau.
/// </summary>
public class StatisticsService(IStatisticsRepository repository, ICampaignRepository campaigns)
    : IStatisticsService
{
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var s = await repository.GetDashboardAsync(ct);

        return new DashboardDto(
            s.TotalCampaigns, s.OpenCampaigns, s.ActiveCampaigns,
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

    public async Task<IReadOnlyList<CampaignProgressDto>> GetCampaignProgressAsync(
        CancellationToken ct = default) =>
        [.. (await campaigns.GetProgressAsync(null, ct)).Select(p => new CampaignProgressDto(
            p.CampaignId, p.CampaignName, p.CampaignTargetQty, p.StartDate, p.EndDate,
            p.CampaignStatus, p.AllocatedTaskQty, p.RemainingTaskQty,
            p.TaskCount, p.CompletedTaskCount))];
}
