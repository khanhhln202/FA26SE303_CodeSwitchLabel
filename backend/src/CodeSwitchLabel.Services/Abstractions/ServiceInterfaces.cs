using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<LoginResponse> RegisterSpeakerAsync(RegisterSpeakerRequest request, CancellationToken ct = default);
    Task<CurrentUserDto> GetCurrentUserAsync(long userId, CancellationToken ct = default);
}

public interface IScriptService
{
    Task<PagedResult<ScriptListItemDto>> SearchAsync(ScriptSearchRequest request, CancellationToken ct = default);

    Task<ScriptDetailDto> GetAsync(string scriptId, CancellationToken ct = default);

    /// <summary>Admin thêm tay một cặp câu — vào thẳng trạng thái đã duyệt kèm lượt duyệt tự động.</summary>
    Task<ScriptDetailDto> CreateAsync(CreateScriptRequest request, long createdById, CancellationToken ct = default);

    /// <summary>Speaker hoặc Reviewer đóng góp một cặp câu — nằm chờ duyệt nội dung.</summary>
    Task<ScriptDetailDto> ContributeAsync(CreateScriptRequest request, long contributorId, CancellationToken ct = default);

    /// <summary>
    /// Admin sửa nội dung một cặp câu đang CHỜ DUYỆT, không ghi lượt duyệt nào nên câu
    /// vẫn ở Chờ duyệt và sửa tiếp được. Câu đã duyệt thì không dùng đường này.
    /// </summary>
    Task<ScriptDetailDto> UpdatePendingAsync(
        string scriptId, UpdateScriptContentRequest request, CancellationToken ct = default);

    /// <summary>Nhập hàng loạt từ file input_text.json. Câu hợp lệ vào thẳng đã duyệt kèm lượt duyệt tự động; câu hỏng không làm hỏng cả file.</summary>
    Task<ImportResultDto> ImportAsync(
        Stream jsonFile, string fileName, long importedById, CancellationToken ct = default);

    /// <summary>Chấp nhận hàng loạt câu đang chờ duyệt của một batch nhập.</summary>
    /// <remarks>
    /// Người gọi (Admin) là người duyệt của từng lượt `Accepted`. Dùng luồng duyệt hiện có
    /// (`ReviewAsync`) nên vẫn giữ đủ quy tắc: chủ đề, phân công hàng đợi, lịch sử duyệt và
    /// trạng thái câu. Lỗi của từng câu được ghi lại, không rollback các câu đã duyệt.
    /// Ném `NotFoundException` (`batch_not_found`) nếu batch không tồn tại.
    /// </remarks>
    Task<BatchValidateResultDto> ValidateBatchAsync(
        long batchId, long reviewerId, CancellationToken ct = default);

    Task<PagedResult<ImportBatchDto>> ListBatchesAsync(PageRequest request, CancellationToken ct = default);

    Task<ImportBatchDetailDto> GetBatchAsync(long batchId, CancellationToken ct = default);

    /// <summary>Duyệt nội dung: chấp nhận, sửa cả cặp, hoặc từ chối kèm lý do.</summary>
    Task<ScriptDetailDto> ReviewAsync(
        string scriptId, long userId, ReviewScriptRequest request, CancellationToken ct = default);

    /// <summary>
    /// Xoá CỨNG một câu đang chờ duyệt. Chặn nếu câu đã duyệt hoặc đã có bản ghi âm —
    /// câu đã thu phải dùng đường xoá mềm/discard-pair để không làm mồ côi audio.
    /// </summary>
    Task DeletePendingAsync(string scriptId, CancellationToken ct = default);

    /// <summary>
    /// Ngừng dùng một câu (xoá mềm): PendingValidation/Validated → Deactivated.
    /// Chặn nếu câu đã có bản ghi âm.
    /// </summary>
    Task<ScriptDetailDto> DeactivateAsync(string scriptId, CancellationToken ct = default);

    /// <summary>Khôi phục câu đã bị loại/ngừng dùng (Rejected/Deactivated) về chờ duyệt.</summary>
    Task<ScriptDetailDto> ReactivateAsync(string scriptId, CancellationToken ct = default);
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
    Task<SystemConfigDto> GetAsync(string key, CancellationToken ct = default);
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

/// <summary>Chiến dịch thu thập — task có thể độc lập (campaign_id NULL) rồi gắn sau.</summary>
public interface ICampaignService
{
    Task<PagedResult<CampaignListItemDto>> SearchAsync(
        CampaignSearchRequest request, long? userId, RoleName? userRole, CancellationToken ct = default);

    Task<CampaignDto> GetAsync(
        long campaignId, long? callerId = null, bool isAdmin = false, CancellationToken ct = default);

    Task<CampaignDto> CreateAsync(
        CreateCampaignRequest request, long createdById, bool isAdmin, CancellationToken ct = default);

    Task<CampaignDto> UpdateAsync(
        long campaignId, UpdateCampaignRequest request, long? callerId, bool isAdmin, CancellationToken ct = default);

    Task<CampaignDto> AssignAsync(
        long campaignId, long? assignedToUserId, CancellationToken ct = default);

    /// <summary>
    /// Xoá chiến dịch. Task đang gắn KHÔNG bị xoá: FK ON DELETE SET NULL nên chúng
    /// tự thành task độc lập và vẫn xoá được ở trang quản lý nhiệm vụ.
    /// </summary>
    Task DeleteAsync(
        long campaignId, long? callerId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Xoá vĩnh viễn một task khỏi chiến dịch; bản ghi thu âm và lượt duyệt được giữ lại.</summary>
    Task DeleteTaskAsync(
        long campaignId, long taskId, long? callerId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Lấy task thu âm duy nhất của chiến dịch; chưa có thì tạo với chỉ tiêu bằng cả chiến dịch.
    /// Idempotent và có thể tự lấp cặp câu đã duyệt nội dung.
    /// </summary>
    Task<TaskDetailDto> GetOrCreateRecordingTaskAsync(
        long campaignId, bool autoFill, long callerId, bool isAdmin, CancellationToken ct = default);
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
    ScriptDomain Domain,
    int CampaignTargetQty,
    DateOnly StartDate,
    DateOnly EndDate,
    CampaignStatus CampaignStatus,
    long? AssignedTo,
    long AllocatedTaskQty,
    long RemainingTaskQty,
    long TaskCount,
    long CompletedTaskCount);
