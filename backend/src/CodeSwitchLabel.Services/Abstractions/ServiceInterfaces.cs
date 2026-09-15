using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<CurrentUserDto> GetCurrentUserAsync(long userId, CancellationToken ct = default);
}

public interface IScriptService
{
    Task<PagedResult<ScriptListItemDto>> SearchAsync(
        ScriptSearchRequest request, CancellationToken ct = default);

    Task<ScriptDetailDto> GetAsync(long id, CancellationToken ct = default);

    /// <summary>Admin thêm script — vào thẳng trạng thái Validated.</summary>
    Task<ScriptDetailDto> CreateAsync(
        CreateScriptRequest request, long createdById, CancellationToken ct = default);

    /// <summary>Speaker đóng góp script — vào PendingValidation, chờ duyệt.</summary>
    Task<ScriptDetailDto> ContributeAsync(
        CreateScriptRequest request, long contributorId, CancellationToken ct = default);

    /// <summary>
    /// Ghi nhận một lượt duyệt nội dung script và cập nhật trạng thái script theo đó.
    /// Chấp nhận hoặc sửa thì script thành Validated; từ chối thì thành Rejected.
    /// </summary>
    Task<ScriptDetailDto> ReviewAsync(
        long scriptId, long userId, ReviewScriptRequest request, CancellationToken ct = default);
}

public interface IScriptAssignmentService
{
    /// <summary>
    /// Script tiếp theo cho Speaker này. Trả null khi hết — trạng thái bình thường,
    /// không phải lỗi, nên tầng Api đổi thành 204 chứ không phải 404.
    /// </summary>
    Task<NextScriptDto?> GetNextAsync(long speakerId, long? taskId, CancellationToken ct = default);

    /// <summary>Speaker bỏ qua script. Ghi lại để không phát lại cho chính người này.</summary>
    Task SkipAsync(long scriptId, long speakerId, long? taskId, CancellationToken ct = default);
}

public interface ISystemConfigService
{
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<IReadOnlyList<SystemConfigDto>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(string key, string value, long updatedById, CancellationToken ct = default);
}

public interface IReasonService
{
    Task<IReadOnlyList<ReasonDto>> GetRejectionReasonsAsync(
        bool activeOnly, CancellationToken ct = default);

    Task<IReadOnlyList<ReasonDto>> GetScriptErrorReasonsAsync(
        bool activeOnly, CancellationToken ct = default);
}

public record SystemConfigDto(
    string Key, string Value, string ValueType, string? Description, DateTimeOffset UpdatedAt);
