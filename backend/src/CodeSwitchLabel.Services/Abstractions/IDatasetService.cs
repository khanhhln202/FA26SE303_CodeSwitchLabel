using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Abstractions;

/// <summary>Dataset xuất dữ liệu: gom bản duyệt đạt, phát hành, tải về.</summary>
public interface IDatasetService
{
    Task<CreateDatasetResult> CreateAsync(
        CreateDatasetRequest request, long createdById, CancellationToken ct = default);
    Task<PagedResult<DatasetDto>> SearchAsync(PageRequest request, CancellationToken ct = default);
    Task<DatasetDetailDto> GetAsync(long datasetId, CancellationToken ct = default);
    Task<DatasetDto> ReleaseAsync(
        long datasetId, ReleaseDatasetRequest request, long releasedById, CancellationToken ct = default);
    Task<DatasetDto> ArchiveAsync(long datasetId, CancellationToken ct = default);
    Task DeleteAsync(long datasetId, CancellationToken ct = default);
    Task<DatasetFileDto> DownloadAsync(
        long datasetId, bool includeAudio = true, CancellationToken ct = default);

    /// <summary>Xuất thẳng ZIP dữ liệu đã duyệt theo Speaker hoặc theo chiến dịch.</summary>
    Task<DatasetFileDto> ExportSpeakersAsync(
        ExportSpeakerDataRequest request, CancellationToken ct = default);
}
