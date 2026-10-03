using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

public record CampaignDto(
    long CampaignId,
    string CampaignName,
    int TargetQty,
    DateOnly StartDate,
    DateOnly EndDate,
    CampaignStatus Status,
    long CreatedBy,
    DateTimeOffset CreatedAt);

/// <param name="AllocatedTaskQty">Tổng chỉ tiêu đã chia cho các task trong chiến dịch.</param>
public record CampaignListItemDto(
    long CampaignId,
    string CampaignName,
    int TargetQty,
    DateOnly StartDate,
    DateOnly EndDate,
    CampaignStatus Status,
    DateTimeOffset CreatedAt,
    long AllocatedTaskQty,
    int TaskCount,
    int CompletedTaskCount);

public record CreateCampaignRequest
{
    [Required(ErrorMessage = "Phải nhập tên chiến dịch.")]
    [StringLength(255, MinimumLength = 1)]
    public string CampaignName { get; init; } = string.Empty;

    /// <summary>Chỉ tiêu cặp câu cho cả chiến dịch; lược đồ giới hạn 2000..5000.</summary>
    [Required(ErrorMessage = "Phải đặt chỉ tiêu.")]
    [Range(2000, 5000, ErrorMessage = "Chỉ tiêu chiến dịch phải từ 2000 đến 5000 cặp câu.")]
    public int? TargetQty { get; init; }

    [Required(ErrorMessage = "Phải chọn ngày bắt đầu.")]
    public DateOnly? StartDate { get; init; }

    [Required(ErrorMessage = "Phải chọn ngày kết thúc.")]
    public DateOnly? EndDate { get; init; }
}

/// <summary>Trường nào để trống thì giữ nguyên.</summary>
public record UpdateCampaignRequest
{
    [StringLength(255, MinimumLength = 1)]
    public string? CampaignName { get; init; }

    [Range(2000, 5000, ErrorMessage = "Chỉ tiêu chiến dịch phải từ 2000 đến 5000 cặp câu.")]
    public int? TargetQty { get; init; }

    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public CampaignStatus? Status { get; init; }
}

public record CampaignSearchRequest : PageRequest
{
    public CampaignStatus? Status { get; init; }
}
