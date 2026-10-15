using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Services.Implementations;

public class DatasetService(
    CodeSwitchLabelDbContext db,
    IObjectStorage storage,
    TimeProvider clock) : IDatasetService
{
    public async Task<CreateDatasetResult> CreateAsync(
        CreateDatasetRequest request, long createdById, CancellationToken ct = default)
    {
        var name = request.DatasetName.Trim();
        var version = request.Version.Trim();

        var dupe = await db.Datasets.AsNoTracking()
            .AnyAsync(d => d.DatasetName == name && d.Version == version, ct);

        if (dupe)
        {
            throw new ConflictException(
                "dataset_exists", $"Dataset {name} phiên bản {version} đã tồn tại.");
        }

        var wanted = request.RecordingIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct()
            .ToList();

        // Gom thêm bản duyệt đạt trong các chiến dịch chỉ định.
        if (request.CampaignIds.Count > 0)
        {
            var fromCampaigns = await db.Recordings.AsNoTracking()
                .Where(r => r.Status == RecordingStatus.Approved)
                .Where(r => r.Task != null && r.Task.CampaignId.HasValue && request.CampaignIds.Contains(r.Task.CampaignId.Value))
                .Select(r => r.RecordingId)
                .ToListAsync(ct);

            wanted = [.. wanted.Union(fromCampaigns)];
        }

        var skipped = new List<SkippedItemDto>();
        var accepted = new List<string>();

        if (wanted.Count > 0)
        {
            var found = await db.Recordings.AsNoTracking()
                .Where(r => wanted.Contains(r.RecordingId))
                .Select(r => new { r.RecordingId, r.Status })
                .ToListAsync(ct);

            var byId = found.ToDictionary(x => x.RecordingId, x => x.Status);

            foreach (var id in wanted)
            {
                if (!byId.TryGetValue(id, out var status))
                {
                    skipped.Add(new SkippedItemDto(id, "recording_not_found"));
                }
                else if (status != RecordingStatus.Approved)
                {
                    skipped.Add(new SkippedItemDto(id, $"status={status}, chỉ nhận bản duyệt đạt"));
                }
                else
                {
                    accepted.Add(id);
                }
            }
        }

        var now = clock.GetUtcNow();
        var dataset = new Dataset
        {
            DatasetName = name,
            Version = version,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            FilterCriteria = JsonSerializer.Serialize(new
            {
                campaignIds = request.CampaignIds,
                recordingIds = request.RecordingIds
            }),
            Status = DatasetStatus.Draft,
            // Đếm ngay số bản vừa nhận. Trước đây để 0 rồi chỉ chốt lại lúc phát hành, nên mọi
            // dataset ở trạng thái nháp đều hiện "0 bản ghi" dù đã có bản trong đó.
            RecordingCount = accepted.Count,
            CreatedAt = now
        };

        db.Datasets.Add(dataset);
        await db.SaveChangesAsync(ct);

        foreach (var id in accepted)
        {
            db.DatasetRecordings.Add(new DatasetRecording
            {
                DatasetId = dataset.DatasetId,
                RecordingId = id,
                IncludedAt = now
            });
        }

        await db.SaveChangesAsync(ct);

        return new CreateDatasetResult(ToDto(dataset), accepted.Count, skipped);
    }

    public async Task<PagedResult<DatasetDto>> SearchAsync(PageRequest request, CancellationToken ct = default)
    {
        var query = db.Datasets.AsNoTracking();
        var total = await query.CountAsync(ct);

        var sort = request.ToSort<DatasetSortField>();

        // TEAM_012: nhánh cuối là thứ tự cũ (mới nhất trước).
        var ordered = sort.Field switch
        {
            DatasetSortField.DatasetId => sort.Descending
                ? query.OrderByDescending(d => d.DatasetId)
                : query.OrderBy(d => d.DatasetId),
            DatasetSortField.CreatedAt => sort.Descending
                ? query.OrderByDescending(d => d.CreatedAt)
                : query.OrderBy(d => d.CreatedAt),
            DatasetSortField.Status => sort.Descending
                ? query.OrderByDescending(d => d.Status)
                : query.OrderBy(d => d.Status),
            DatasetSortField.DatasetName => sort.Descending
                ? query.OrderByDescending(d => d.DatasetName)
                : query.OrderBy(d => d.DatasetName),
            DatasetSortField.RecordingCount => sort.Descending
                ? query.OrderByDescending(d => d.RecordingCount)
                : query.OrderBy(d => d.RecordingCount),
            _ => query.OrderByDescending(d => d.CreatedAt)
        };

        var items = await ordered
            .ThenBy(d => d.DatasetId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PagedResult<DatasetDto>(
            [.. items.Select(ToDto)], request.Page, request.PageSize, total);
    }

    public async Task<DatasetDetailDto> GetAsync(long datasetId, CancellationToken ct = default)
    {
        var dataset = await db.Datasets.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DatasetId == datasetId, ct)
            ?? throw new NotFoundException("dataset_not_found", $"Không tìm thấy dataset #{datasetId}.");

        return new DatasetDetailDto(ToDto(dataset), await MissingPairsAsync(datasetId, ct));
    }

    public async Task<DatasetDto> ReleaseAsync(
        long datasetId, ReleaseDatasetRequest request, long releasedById, CancellationToken ct = default)
    {
        var dataset = await db.Datasets.FirstOrDefaultAsync(d => d.DatasetId == datasetId, ct)
            ?? throw new NotFoundException("dataset_not_found", $"Không tìm thấy dataset #{datasetId}.");

        if (dataset.Status == DatasetStatus.Released) return ToDto(dataset);

        if (dataset.Status != DatasetStatus.Draft)
        {
            throw new ConflictException(
                "dataset_not_releasable", $"Dataset #{datasetId} đang ở trạng thái {dataset.Status}.");
        }

        var missing = await MissingPairsAsync(datasetId, ct);
        if (missing.Count > 0)
        {
            throw new UnprocessableException(
                "dataset_incomplete",
                $"Dataset thiếu một biến thể ở {missing.Count} cặp câu " +
                $"({string.Join(", ", missing.Take(5).Select(m => m.ScriptId))}...). " +
                "Mỗi cặp câu cần đủ cả hai bản cs và vi.");
        }

        var count = await db.DatasetRecordings.CountAsync(dr => dr.DatasetId == datasetId, ct);

        dataset.Status = DatasetStatus.Released;
        dataset.ReleasedBy = releasedById;
        dataset.ReleasedAt = clock.GetUtcNow();
        dataset.FileFormat = request.FileFormat;
        dataset.FileKey = $"datasets/{datasetId}/{dataset.DatasetName}-{dataset.Version}.zip";
        dataset.RecordingCount = count;

        await db.SaveChangesAsync(ct);

        return ToDto(dataset);
    }

    public async Task<DatasetDto> ArchiveAsync(long datasetId, CancellationToken ct = default)
    {
        var dataset = await db.Datasets.FirstOrDefaultAsync(d => d.DatasetId == datasetId, ct)
            ?? throw new NotFoundException("dataset_not_found", $"Không tìm thấy dataset #{datasetId}.");

        dataset.Status = DatasetStatus.Archived;
        await db.SaveChangesAsync(ct);

        return ToDto(dataset);
    }

    public async Task<DatasetFileDto> DownloadAsync(long datasetId, CancellationToken ct = default)
    {
        var dataset = await db.Datasets.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DatasetId == datasetId, ct)
            ?? throw new NotFoundException("dataset_not_found", $"Không tìm thấy dataset #{datasetId}.");

        if (dataset.Status != DatasetStatus.Released)
        {
            throw new ConflictException(
                "dataset_not_released", $"Dataset #{datasetId} chưa phát hành nên chưa tải được.");
        }

        var rows = await db.DatasetRecordings.AsNoTracking()
            .Include(dr => dr.Recording).ThenInclude(r => r.Script)
            .Where(dr => dr.DatasetId == datasetId)
            .OrderBy(dr => dr.Recording.ScriptId).ThenBy(dr => dr.Recording.RecordingId)
            .ToListAsync(ct);

        // Manifest: mỗi bản ghi kèm câu đối chiếu và link nghe tạm (15 phút).
        // File âm thanh gốc nằm ở kho S3/MinIO — ZIP này mang link, không đóng gói nhị phân,
        // để file tải về nhẹ và link luôn mới.
        var manifest = new List<object>();
        var csv = new StringBuilder();
        csv.AppendLine("recording_id,script_id,variant,speaker_id,duration_sec,audio_url");

        foreach (var dr in rows)
        {
            var r = dr.Recording;
            string? url = null;
            try
            {
                var key = storage.GetObjectKey(r.CloudLink);
                if (key is not null) url = (await storage.GetDownloadUrlAsync(key, ct)).Url;
            }
            catch { /* thiếu link thì manifest vẫn có metadata */ }

            manifest.Add(new
            {
                recordingId = r.RecordingId,
                scriptId = r.ScriptId,
                variant = r.SentenceVariant.ToString(),
                speakerId = r.SpeakerId,
                csContent = r.Script.CsContent,
                veContent = r.Script.ViContent,
                durationSec = r.DurationSec,
                audioUrl = url
            });

            csv.AppendLine($"{r.RecordingId},{r.ScriptId},{r.SentenceVariant},{r.SpeakerId},{r.DurationSec},{url}");
        }

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = zip.CreateEntry("manifest.json");
            await using (var entry = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(entry, new
                {
                    dataset = dataset.DatasetName,
                    version = dataset.Version,
                    releasedAt = dataset.ReleasedAt,
                    recordings = manifest
                }, cancellationToken: ct);
            }

            var csvEntry = zip.CreateEntry("metadata.csv");
            await using (var entry = csvEntry.Open())
            {
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                await entry.WriteAsync(bytes, ct);
            }
        }

        return new DatasetFileDto(
            $"{dataset.DatasetName}-{dataset.Version}.zip",
            "application/zip",
            stream.ToArray());
    }

    // ----------------------------------------------------------------- nội bộ

    private async Task<List<DatasetMissingPairDto>> MissingPairsAsync(long datasetId, CancellationToken ct)
    {
        // Gom ở ứng dụng chứ không trong SQL.
        //
        // Cách viết trước đây (GroupBy trong SQL rồi Any lồng bên trong Select) EF Core dịch
        // không nổi và ném InvalidOperationException — tức mọi lần phát hành dataset lẫn mọi
        // lần xem chi tiết đều thành lỗi 500. Chỉ cần đưa phần "gom theo cặp (câu, biến thể)"
        // xuống đây là xong; một dataset chỉ chứa vài chục bản nên gom trong bộ nhớ rẻ hơn là
        // viết SQL thủ công.
        var pairs = await db.DatasetRecordings.AsNoTracking()
            .Where(dr => dr.DatasetId == datasetId)
            .Select(dr => new { dr.Recording.ScriptId, dr.Recording.SentenceVariant })
            .Distinct()
            .ToListAsync(ct);

        return
        [
            .. pairs
                .GroupBy(p => p.ScriptId)
                .Select(g => new DatasetMissingPairDto(
                    g.Key,
                    g.Any(p => p.SentenceVariant == SentenceVariant.CodeSwitching),
                    g.Any(p => p.SentenceVariant == SentenceVariant.PureVietnamese)))
                .Where(x => !x.HasCs || !x.HasVi)
        ];
    }

    private static DatasetDto ToDto(Dataset d) => new(
        d.DatasetId, d.DatasetName, d.Version, d.Description, d.Status,
        d.RecordingCount, d.ReleasedBy, d.ReleasedAt, d.FileKey, d.FileFormat, d.CreatedAt);
}
