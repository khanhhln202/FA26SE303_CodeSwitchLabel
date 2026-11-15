using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>Danh sách bản ghi âm cho Admin: mỗi dòng là MỘT CẶP, trạng thái do BE tính.</summary>
public interface IAdminRecordingService
{
    Task<PagedResult<AdminRecordingPairDto>> GetPairsAsync(
        PageRequest query, CancellationToken ct = default);
}
