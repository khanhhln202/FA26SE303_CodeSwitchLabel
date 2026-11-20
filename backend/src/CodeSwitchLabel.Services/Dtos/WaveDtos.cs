using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

public record WaveDto(
    long WaveId,
    string WaveName,
    string Domain,
    int TargetQty,
    DateOnly StartDate,
    DateOnly EndDate,
    WaveStatus Status,
    long CreatedBy,
    long? AssignedTo,
    DateTimeOffset CreatedAt,
    int BufferQty,
    int ImportedQty);

public record AvailableScriptsDto(string Domain, string TopicName, int ValidatedTotal, int RecordedPairs, int AvailablePairs);

public record AvailableScriptCountDto(string Domain, int Count);

/// <param name="AllocatedTaskQty">Tổng chỉ tiêu đã chia cho các task trong chiến dịch.</param>
/// <param name="TaskCount">Số task đang gắn; xoá chiến dịch sẽ gỡ đúng số này về task độc lập.</param>
public record WaveListItemDto(
    long WaveId,
    string WaveName,
    string Domain,
    int TargetQty,
    DateOnly StartDate,
    DateOnly EndDate,
    WaveStatus Status,
    DateTimeOffset CreatedAt,
    long AllocatedTaskQty,
    int TaskCount,
    int CompletedTaskCount,
    long? AssignedTo);

public record CreateWaveRequest
{
    [Required(ErrorMessage = "Phải nhập tên chiến dịch.")]
    [StringLength(255, MinimumLength = 1)]
    public string WaveName { get; init; } = string.Empty;

    /// <summary>Chủ đề của cả đợt; chỉ nhiệm vụ cùng chủ đề mới gắn vào được.</summary>
    [Required(ErrorMessage = "Phải chọn chủ đề (domain).")]
    public string? Domain { get; init; }

    /// <summary>Chỉ tiêu cặp câu cho cả chiến dịch; lược đồ giới hạn 2000..5000.</summary>
    [Required(ErrorMessage = "Phải đặt chỉ tiêu.")]
    [Range(2000, 5000, ErrorMessage = "Chỉ tiêu chiến dịch phải từ 2000 đến 5000 cặp câu.")]
    public int? TargetQty { get; init; }

    /// <summary>Số câu dự phòng thêm vào đợt khi bài bị từ chối, nhập thêm đè trên chỉ tiêu task.</summary>
    [Range(0, 10_000, ErrorMessage = "Đệm phải từ 0 đến 10.000 câu.")]
    public int? BufferQty { get; init; }

    [Required(ErrorMessage = "Phải chọn ngày bắt đầu.")]
    public DateOnly? StartDate { get; init; }

    [Required(ErrorMessage = "Phải chọn ngày kết thúc.")]
    public DateOnly? EndDate { get; init; }
}

/// <summary>Trường nào để trống thì giữ nguyên.</summary>
public record UpdateWaveRequest
{
    [StringLength(255, MinimumLength = 1)]
    public string? WaveName { get; init; }

    /// <summary>
    /// Đổi chủ đề chỉ được khi đợt chưa có task khác chủ đề — database chặn,
    /// vì mọi task đang gắn đều phải cùng chủ đề với đợt.
    /// </summary>
    public string? Domain { get; init; }

    [Range(2000, 5000, ErrorMessage = "Chỉ tiêu chiến dịch phải từ 2000 đến 5000 cặp câu.")]
    public int? TargetQty { get; init; }

    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }

    /// <summary>
    /// không còn trường <c>status</c>. Trạng thái đợt là hàm của các task bên
    /// trong (chưa có nhiệm vụ → Open, có nhiệm vụ → InProgress, mọi nhiệm vụ xong →
    /// Completed), nên không có đường nào để bấm tay đặt nó.
    /// </summary>
}

public record WaveSearchRequest : PageRequest
{
    public WaveStatus? Status { get; init; }
}

/// <summary>Nhân khẩu rút gọn của một Speaker tham gia chiến dịch.</summary>
public record WaveSpeakerBriefDto(
    long UserId, string FullName, Gender? Gender, SpeakerRegion? Region, short? BirthYear);

/// <summary>Dashboard một chiến dịch: quy mô câu, thời lượng và nhân khẩu người đọc.</summary>
public record WaveDashboardDto(
    long WaveId,
    string WaveName,
    string Domain,
    string DomainName,
    int SpeakerCount,
    IReadOnlyList<WaveSpeakerBriefDto> Speakers,
    int SentencePairs,
    int ApprovedPairs,
    double AudioHours,
    IReadOnlyDictionary<string, int> GenderCounts,
    IReadOnlyDictionary<string, int> RegionCounts,
    IReadOnlyDictionary<string, int> AgeGroups);
