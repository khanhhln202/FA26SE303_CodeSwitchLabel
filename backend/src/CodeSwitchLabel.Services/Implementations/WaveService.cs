using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Implementations;

/// <summary>
/// Quản lý chiến dịch thu thập — task có thể độc lập (wave_id NULL) rồi gắn sau.
/// Các ràng buộc chỉ tiêu/ngày với task con (khi đang gắn) phần lớn do trigger của database chặn;
/// ở đây kiểm trước những trường hợp dễ gặp để trả thông báo rõ ràng thay vì lỗi thô từ database.
/// </summary>
public class WaveService(
    IWaveRepository waves,
    ITaskService tasks,
    TimeProvider clock) : IWaveService
{
    public async Task<PagedResult<WaveListItemDto>> SearchAsync(
        WaveSearchRequest request, long? userId, RoleName? userRole, CancellationToken ct = default)
    {
        long? assignedTo = null;
        if (userRole == RoleName.TaskManager && userId.HasValue)
        {
            assignedTo = userId.Value;
        }

        var (items, total) = await waves.SearchAsync(
            request.Status, assignedTo, request.Page, request.PageSize,
            request.ToSort<WaveSortField>(), ct);

        var ids = items.Select(c => c.WaveId).ToList();
        var progress = (await waves.GetProgressAsync(ids, ct))
            .ToDictionary(p => p.WaveId);

        var mapped = items.Select(c =>
        {
            progress.TryGetValue(c.WaveId, out var p);

            // Truyền c.Domain sang WaveListItemDto
            return new WaveListItemDto(
                c.WaveId, c.WaveName, c.Domain, c.TargetQty, c.StartDate, c.EndDate, c.Status, c.CreatedAt,
                p?.AllocatedTaskQty ?? 0, (int)(p?.TaskCount ?? 0), (int)(p?.CompletedTaskCount ?? 0), c.AssignedTo);
        }).ToList();

        return new PagedResult<WaveListItemDto>(mapped, request.Page, request.PageSize, total);
    }

    public async Task<WaveDto> GetAsync(
        long waveId, long? callerId = null, bool isAdmin = false, CancellationToken ct = default)
    {
        var wave = await waves.GetAsync(waveId, ct) ?? throw NotFound(waveId);
        EnsureWaveOwner(wave, callerId, isAdmin);
        return ToDto(wave);
    }

    // Hỗ trợ Task Manager tạo đợt (auto-assign cho chính mình nếu không phải Admin)
    public async Task<WaveDto> CreateAsync(
        CreateWaveRequest request, long createdById, bool isAdmin, CancellationToken ct = default)
    {
        var start = request.StartDate!.Value;
        var end = request.EndDate!.Value;

        if (start > end)
        {
            throw new UnprocessableException(
                "wave_dates_invalid", "Ngày bắt đầu chiến dịch phải trước hoặc bằng ngày kết thúc.");
        }

        var wave = new Wave
        {
            WaveName = request.WaveName.Trim(),
            Domain = request.Domain ?? throw new UnprocessableException("domain_required", "Phải chọn chủ đề (domain) cho chiến dịch."),
            TargetQty = request.TargetQty!.Value,
            StartDate = start,
            EndDate = end,
            // đợt mới tạo chưa có nhiệm vụ nào nên là Open. Sau đó trạng thái
            // đợt là hàm của các task, tự tính lại ở WaveRepository.RefreshStatusAsync.
            Status = WaveStatus.Open,
            CreatedBy = createdById,
            // Task Manager tự tạo đợt thì tự động gán cho chính mình để có quyền tạo/gắn task
            AssignedTo = isAdmin ? null : createdById,
            CreatedAt = clock.GetUtcNow()
        };

        waves.Add(wave);
        await waves.SaveChangesAsync(ct);

        return ToDto(wave);
    }

    // Kiểm tra quyền sở hữu khi sửa đợt (TaskManager chỉ sửa đợt của mình)
    public async Task<WaveDto> UpdateAsync(
        long waveId, UpdateWaveRequest request, long? callerId, bool isAdmin, CancellationToken ct = default)
    {
        var wave = await waves.GetForUpdateAsync(waveId, ct) ?? throw NotFound(waveId);
        EnsureWaveOwner(wave, callerId, isAdmin);

        if (request.WaveName is not null) wave.WaveName = request.WaveName.Trim();
        if (request.Domain.HasValue) wave.Domain = request.Domain.Value;

        // UpdateWaveRequest không còn trường `status`. Trạng thái đợt là hàm
        // của các task bên trong nên không bấm tay được — sửa tên/chủ đề/ngày không được đổi nó,
        // và nó cũng không cần đổi vì những thay đổi đó không đụng tới task.

        if (request.StartDate.HasValue) wave.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) wave.EndDate = request.EndDate.Value;

        if (wave.StartDate > wave.EndDate)
        {
            throw new UnprocessableException(
                "wave_dates_invalid", "Ngày bắt đầu chiến dịch phải trước hoặc bằng ngày kết thúc.");
        }

        if (request.TargetQty.HasValue)
        {
            // Không hạ chỉ tiêu xuống dưới phần đã chia cho các task — trigger của database cũng chặn,
            // nhưng kiểm ở đây để trả lỗi rõ ràng.
            var allocated = await waves.SumAllocatedAsync(waveId, ct);

            if (request.TargetQty.Value < allocated)
            {
                throw new UnprocessableException(
                    "wave_target_below_allocated",
                    $"Chỉ tiêu {request.TargetQty.Value} nhỏ hơn {allocated} đã chia cho các task của chiến dịch.");
            }

            wave.TargetQty = request.TargetQty.Value;
        }

        await waves.SaveChangesAsync(ct);
        return ToDto(wave);
    }

    // Xoá chiến dịch — task con thành độc lập nhờ ON DELETE SET NULL
    public async Task DeleteAsync(
        long waveId, long? callerId, bool isAdmin, CancellationToken ct = default)
    {
        var wave = await waves.GetForUpdateAsync(waveId, ct) ?? throw NotFound(waveId);
        EnsureWaveOwner(wave, callerId, isAdmin);

        waves.Remove(wave);
        await waves.SaveChangesAsync(ct);
    }

    public async Task<WaveDto> AssignAsync(
        long waveId, long? assignedToUserId, CancellationToken ct = default)
    {
        var wave = await waves.GetForUpdateAsync(waveId, ct) ?? throw NotFound(waveId);

        if (assignedToUserId.HasValue)
        {
            var isTaskManager = await waves.IsTaskManagerAsync(assignedToUserId.Value, ct);
            if (!isTaskManager)
            {
                throw new UnprocessableException(
                    "assigned_to_invalid_role",
                    "Chỉ giao được chiến dịch cho Task Manager. Người bạn chọn đang giữ vai khác.");
            }
        }

        wave.AssignedTo = assignedToUserId;

        await waves.SaveChangesAsync(ct);
        return ToDto(wave);
    }

    /// <summary>Xoá vĩnh viễn task khỏi chiến dịch. Giữ lại bản ghi thu âm và lượt duyệt.</summary>
    public Task DeleteTaskAsync(
        long waveId, long taskId, long? callerId, bool isAdmin, CancellationToken ct = default)
        => tasks.DeleteFromWaveAsync(waveId, taskId, ct, callerId, isAdmin);

    /// <summary>Task thu âm duy nhất của chiến dịch, tạo nếu chưa có.</summary>
    public Task<TaskDetailDto> GetOrCreateRecordingTaskAsync(
        long waveId, bool autoFill, long callerId, bool isAdmin, CancellationToken ct = default)
        => tasks.GetOrCreateRecordingTaskAsync(waveId, autoFill, callerId, isAdmin, ct);

    private static void EnsureWaveOwner(Wave wave, long? callerId, bool isAdmin)
    {
        if (!callerId.HasValue || isAdmin) return;
        if (wave.AssignedTo == callerId.Value || wave.CreatedBy == callerId.Value) return;

        throw new ForbiddenException(
            "wave_not_owned",
            $"Chiến dịch #{wave.WaveId} không thuộc quyền quản lý của bạn.");
    }

    private static WaveDto ToDto(Wave c) =>
        new(c.WaveId, c.WaveName, c.Domain, c.TargetQty, c.StartDate, c.EndDate, c.Status, c.CreatedBy, c.AssignedTo, c.CreatedAt);

    private static NotFoundException NotFound(long waveId) =>
        new("wave_not_found", $"Không tìm thấy chiến dịch #{waveId}.");
}
