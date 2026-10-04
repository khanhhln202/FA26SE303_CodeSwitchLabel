using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<CurrentUserDto> GetCurrentUserAsync(long userId, CancellationToken ct = default);
}

public interface IScriptService
{
    Task<PagedResult<ScriptListItemDto>> SearchAsync(ScriptSearchRequest request, CancellationToken ct = default);

    Task<ScriptDetailDto> GetAsync(string scriptId, CancellationToken ct = default);

    /// <summary>Admin thêm tay một cặp câu — vào thẳng trạng thái đã duyệt.</summary>
    Task<ScriptDetailDto> CreateAsync(CreateScriptRequest request, long createdById, CancellationToken ct = default);

    /// <summary>Speaker đóng góp một cặp câu — nằm chờ duyệt nội dung.</summary>
    Task<ScriptDetailDto> ContributeAsync(CreateScriptRequest request, long contributorId, CancellationToken ct = default);

    /// <summary>Nhập hàng loạt từ file input_text.json.</summary>
    Task<ImportResultDto> ImportAsync(
        Stream jsonFile, string fileName, long importedById, CancellationToken ct = default);

    /// <summary>Duyệt nội dung: chấp nhận, sửa cả cặp, hoặc từ chối kèm lý do.</summary>
    Task<ScriptDetailDto> ReviewAsync(
        string scriptId, long userId, ReviewScriptRequest request, CancellationToken ct = default);
}

public interface IScriptAssignmentService
{
    /// <summary>Cặp câu tiếp theo để thu âm, kèm danh sách biến thể còn thiếu.</summary>
    Task<NextScriptDto?> GetNextAsync(long speakerId, long? taskId, CancellationToken ct = default);
}

public interface ISystemConfigService
{
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<IReadOnlyList<SystemConfigDto>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(string key, string value, long updatedById, CancellationToken ct = default);
}

public interface IReasonService
{
    Task<IReadOnlyList<ReasonDto>> GetRejectionReasonsAsync(bool activeOnly, CancellationToken ct = default);
    Task<IReadOnlyList<ReasonDto>> GetScriptErrorReasonsAsync(bool activeOnly, CancellationToken ct = default);
}

/// <summary>Số liệu cho màn hình quản trị, đọc thẳng từ các view của lược đồ.</summary>
public interface IStatisticsService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SpeakerQualityDto>> GetSpeakerQualityAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReviewerQualityDto>> GetReviewerQualityAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RejectionStatDto>> GetRejectionStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CampaignProgressDto>> GetCampaignProgressAsync(CancellationToken ct = default);
}

/// <summary>Chiến dịch thu thập — đơn vị kế hoạch cha của mọi task.</summary>
public interface ICampaignService
{
    Task<PagedResult<CampaignListItemDto>> SearchAsync(
        CampaignSearchRequest request, long? userId, RoleName? userRole, CancellationToken ct = default);

    Task<CampaignDto> GetAsync(long campaignId, CancellationToken ct = default);

    Task<CampaignDto> CreateAsync(
        CreateCampaignRequest request, long createdById, CancellationToken ct = default);

    Task<CampaignDto> UpdateAsync(
        long campaignId, UpdateCampaignRequest request, CancellationToken ct = default);

    Task<CampaignDto> AssignAsync(
        long campaignId, long? assignedToUserId, CancellationToken ct = default);
}

public record SystemConfigDto(
    string Key, string Value, string ValueType, string? Description, DateTimeOffset UpdatedAt);

/// <param name="ApprovedDurationSec">Tổng thời lượng các bản đã duyệt đạt, tính bằng giây.</param>
public record DashboardDto(
    long TotalCampaigns,
    long OpenCampaigns,
    long ActiveCampaigns,
    long TotalScripts,
    long ValidatedScripts,
    long PendingScripts,
    long TotalRecordings,
    long ApprovedRecordings,
    long RejectedRecordings,
    decimal ApprovedDurationSec,
    double ApprovedDurationHours,
    long Speakers,
    long Reviewers,
    long ReleasedDatasets);

public record SpeakerQualityDto(
    long UserId, string FullName, long Recordings, long Approved, long Rejected, decimal? ApprovalRatePct);

public record ReviewerQualityDto(
    long UserId, string FullName, long ReviewsDone, long Approvals, long Rejections);

public record RejectionStatDto(string ReasonCode, string Category, long TimesUsed);

/// <summary>Tiến độ một chiến dịch, đọc thẳng từ view v_campaign_progress.</summary>
public record CampaignProgressDto(
    long CampaignId,
    string CampaignName,
    int CampaignTargetQty,
    DateOnly StartDate,
    DateOnly EndDate,
    CampaignStatus CampaignStatus,
    long? AssignedTo,
    long AllocatedTaskQty,
    long RemainingTaskQty,
    long TaskCount,
    long CompletedTaskCount);
