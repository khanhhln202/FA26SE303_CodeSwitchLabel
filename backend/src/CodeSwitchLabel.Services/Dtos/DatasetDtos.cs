using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Repositories.Enums;

namespace CodeSwitchLabel.Services.Dtos;

public record CreateDatasetRequest
{
    [Required(ErrorMessage = "Phải đặt tên dataset.")]
    [StringLength(100, MinimumLength = 1)]
    public string DatasetName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phải đặt phiên bản.")]
    [StringLength(32, MinimumLength = 1)]
    public string Version { get; init; } = "v1";

    [StringLength(2000)]
    public string? Description { get; init; }

    /// <summary>Mã bản ghi chọn tay — chỉ bản đã duyệt đạt mới được nhận.</summary>
    public List<string> RecordingIds { get; init; } = [];

    /// <summary>Gom toàn bộ bản đã duyệt đạt trong các chiến dịch này.</summary>
    public List<long> WaveIds { get; init; } = [];
}

public record ReleaseDatasetRequest
{
    public DatasetFileFormat FileFormat { get; init; } = DatasetFileFormat.Json;
}

public record DatasetDto(
    long DatasetId,
    string DatasetName,
    string Version,
    string? Description,
    DatasetStatus Status,
    int RecordingCount,
    long? ReleasedBy,
    DateTimeOffset? ReleasedAt,
    string? FileKey,
    DatasetFileFormat? FileFormat,
    DateTimeOffset CreatedAt);

public record DatasetMissingPairDto(string ScriptId, bool HasCs, bool HasVi);

public record DatasetDetailDto(
    DatasetDto Summary,
    IReadOnlyList<DatasetMissingPairDto> MissingPairs);

public record CreateDatasetResult(
    DatasetDto Dataset,
    int Added,
    IReadOnlyList<SkippedItemDto> Skipped);

/// <summary>Controller ghi thẳng stream ra response rồi tự dispose — nơi tạo không được dispose.</summary>
public record DatasetFileDto(string FileName, string ContentType, Stream Content);

/// <summary>
/// Xuất trực tiếp dữ liệu đã duyệt, không qua bước tạo/phát hành dataset.
/// Chọn SpeakerIds thì xuất các speaker đó; bỏ trống thì xuất cả một chiến dịch.
/// </summary>
public record ExportSpeakerDataRequest
{
    public List<long> SpeakerIds { get; init; } = [];
    public long? WaveId { get; init; }
}
