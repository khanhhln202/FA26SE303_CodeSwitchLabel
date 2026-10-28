using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodeSwitchLabel.Services.Implementations;

public class DatasetService(
    CodeSwitchLabelDbContext db,
    IObjectStorage storage,
    TimeProvider clock,
    ILogger<DatasetService> logger) : IDatasetService
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

        // Có chọn bản ghi mà không nhận được bản nào (thường do bản ghi chưa đủ lượt duyệt)
        // thì báo lỗi rõ ràng, thay vì tạo một dataset nháp rỗng mà admin tưởng là thành công.
        if (wanted.Count > 0 && accepted.Count == 0)
        {
            var reasons = string.Join("; ", skipped.Take(5).Select(s => $"{s.Id}: {s.Reason}"));
            throw new UnprocessableException(
                "dataset_no_approved_recordings",
                $"Không có bản ghi nào đã duyệt đạt trong lựa chọn. {reasons}");
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

        // nhánh cuối là thứ tự cũ (mới nhất trước).
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

    /// <summary>Xoá cứng dataset nháp. Chỉ nháp mới xoá được — bản đã phát hành/lưu trữ phải giữ dấu vết.</summary>
    public async Task DeleteAsync(long datasetId, CancellationToken ct = default)
    {
        var dataset = await db.Datasets.FirstOrDefaultAsync(d => d.DatasetId == datasetId, ct)
            ?? throw new NotFoundException("dataset_not_found", $"Không tìm thấy dataset #{datasetId}.");

        if (dataset.Status != DatasetStatus.Draft)
        {
            throw new ConflictException(
                "dataset_not_draft",
                $"Chỉ xoá được dataset nháp. Dataset #{datasetId} đang ở trạng thái {dataset.Status}.");
        }

        // Xoá các dòng con trước trong cùng SaveChanges. Trigger chặn sửa dataset_recording
        // đọc trạng thái dataset, nên xoá lúc dataset vẫn còn là Draft là chắc chắn hợp lệ.
        var links = await db.DatasetRecordings
            .Where(dr => dr.DatasetId == datasetId)
            .ToListAsync(ct);

        db.DatasetRecordings.RemoveRange(links);
        db.Datasets.Remove(dataset);
        await db.SaveChangesAsync(ct);
    }

    public async Task<DatasetFileDto> DownloadAsync(
        long datasetId, bool includeAudio = true, CancellationToken ct = default)
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

        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Manifest: mỗi bản ghi kèm câu đối chiếu, file audio trong ZIP và link nghe tạm.
            // Audio gốc nằm ở kho S3/MinIO; khi includeAudio = true thì đóng gói luôn file nhị phân
            // vào audio/ để bản tải về dùng được ngay, không phụ thuộc link hết hạn sau 15 phút.
            var manifest = new List<object>();
            var csv = new StringBuilder();
            csv.AppendLine("recording_id,script_id,variant,speaker_id,duration_sec,audio_file,audio_url");

            foreach (var dr in rows)
            {
                var r = dr.Recording;
                var key = storage.GetObjectKey(r.CloudLink);

                string? url = null;
                if (key is not null)
                {
                    try { url = (await storage.GetDownloadUrlAsync(key, ct)).Url; }
                    catch { /* thiếu link thì manifest vẫn có metadata */ }
                }

                var audioFile = includeAudio && key is not null
                    ? await TryAddAudioAsync(zip, r.RecordingId, key, ct)
                    : null;

                manifest.Add(new
                {
                    recordingId = r.RecordingId,
                    scriptId = r.ScriptId,
                    variant = r.SentenceVariant.ToString(),
                    speakerId = r.SpeakerId,
                    csContent = r.Script.CsContent,
                    viContent = r.Script.ViContent,
                    durationSec = r.DurationSec,
                    audioFile,
                    audioUrl = url
                });

                csv.AppendLine($"{r.RecordingId},{r.ScriptId},{r.SentenceVariant},{r.SpeakerId},{r.DurationSec},{audioFile},{url}");
            }

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

        stream.Position = 0;
        return new DatasetFileDto(
            $"{dataset.DatasetName}-{dataset.Version}.zip",
            "application/zip",
            stream);
    }

    public async Task<DatasetFileDto> ExportSpeakersAsync(
        ExportSpeakerDataRequest request, CancellationToken ct = default)
    {
        var speakerIds = request.SpeakerIds.Where(id => id > 0).Distinct().ToList();

        if (speakerIds.Count == 0 && request.CampaignId is null)
        {
            throw new UnprocessableException(
                "export_scope_required", "Chọn ít nhất một Speaker hoặc một chiến dịch để tải dữ liệu.");
        }

        var recordings = await db.Recordings.AsNoTracking()
            .Include(r => r.Script).ThenInclude(s => s.Words)
            .Include(r => r.Speaker)
            .Where(r => r.Status == RecordingStatus.Approved)
            .Where(r => speakerIds.Count == 0 || speakerIds.Contains(r.SpeakerId))
            .Where(r => request.CampaignId == null || (r.Task != null && r.Task.CampaignId == request.CampaignId))
            .ToListAsync(ct);

        // Chỉ đóng gói cặp đủ hai bản. Thiếu một bản thì bỏ cả cặp, không để JSON trỏ tới file không có.
        var pairs = recordings
            .GroupBy(r => new { r.SpeakerId, r.ScriptId })
            .Select(g => new
            {
                Speaker = g.First().Speaker,
                Script = g.First().Script,
                Cs = g.FirstOrDefault(r => r.SentenceVariant == SentenceVariant.CodeSwitching),
                Vi = g.FirstOrDefault(r => r.SentenceVariant == SentenceVariant.PureVietnamese)
            })
            .Where(p => p.Cs is not null && p.Vi is not null)
            .OrderBy(p => p.Speaker.FullName)
            .ThenBy(p => p.Script.ScriptId)
            .ToList();

        if (pairs.Count == 0)
        {
            throw new UnprocessableException(
                "export_no_approved_pairs",
                "Không có cặp câu nào đã duyệt đủ cả hai bản trong phạm vi đã chọn.");
        }

        // Mốc Bangkok (UTC+7) trong tên thư mục, đúng quy ước bàn giao dữ liệu.
        var date = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7))
            .ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);

        // ZIP có thể chứa hàng GB audio nên ghi ra file tạm thay vì giữ cả file trong RAM.
        // DeleteOnClose: FileStreamResult dispose sau khi gửi xong là file tự xoá.
        var stream = new FileStream(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024,
            FileOptions.DeleteOnClose);

        try
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var pair in pairs)
                {
                    var csAudio = await TryReadAudioAsync(pair.Cs!, ct);
                    var viAudio = await TryReadAudioAsync(pair.Vi!, ct);

                    if (csAudio is null || viAudio is null)
                    {
                        logger.LogWarning(
                            "Bỏ cặp {ScriptId} của speaker {SpeakerId} khỏi ZIP vì thiếu file audio.",
                            pair.Script.ScriptId, pair.Speaker.UserId);
                        continue;
                    }

                    var folder = $"{SafeFolderName(pair.Speaker.Email)}_{date}";
                    var scriptId = pair.Script.ScriptId;

                    // Dùng lại đúng DTO của bộ nhập để file JSON luôn khớp schema hiện hành.
                    // File bàn giao đọc bằng mắt nên xuống dòng + giữ tiếng Việt, và id chỉ giữ
                    // phần số đúng mẫu input_text.json (bỏ tiền tố nội bộ s_). Cùng một id số
                    // dùng cho tên file âm thanh để JSON và WAV khớp nhau.
                    var numericId = scriptId.StartsWith("s_", StringComparison.Ordinal) ? scriptId[2..] : scriptId;
                    var json = JsonSerializer.SerializeToUtf8Bytes(new ImportScriptItem
                    {
                        Id = numericId,
                        Domain = DomainLabel(pair.Script.Domain),
                        CsTranscript = pair.Script.CsContent,
                        ViEquivalent = pair.Script.ViContent,
                        Alignment = [.. ScriptPairRules.ToAlignment(pair.Script.Words)]
                    }, ExportJsonOptions);

                    await AddZipEntryAsync(zip, $"{folder}/text/{numericId}.json", json, ct);
                    await AddZipEntryAsync(zip, $"{folder}/audio/{numericId}-cs.wav", csAudio, ct);
                    await AddZipEntryAsync(zip, $"{folder}/audio/{numericId}-vi.wav", viAudio, ct);
                }
            }
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        var fileName = speakerIds.Count == 0
            ? $"campaign-{request.CampaignId}-{date}.zip"
            : $"speakers-{date}.zip";

        stream.Position = 0;
        return new DatasetFileDto(fileName, "application/zip", stream);
    }

    // ----------------------------------------------------------------- nội bộ

    /// <summary>File bàn giao đọc bằng mắt: xuống dòng, không đổi tiếng Việt thành \uXXXX.</summary>
    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Đọc trọn file audio trước khi tạo entry, để ZIP không bao giờ có file cụt.</summary>
    private async Task<byte[]?> TryReadAudioAsync(Recording recording, CancellationToken ct)
    {
        var key = storage.GetObjectKey(recording.CloudLink);
        if (key is null) return null;

        try
        {
            await using var source = await storage.OpenReadAsync(key, ct);
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Không đọc được audio {RecordingId} ({Key}) khi xuất ZIP.", recording.RecordingId, key);
            return null;
        }
    }

    private static async Task AddZipEntryAsync(
        ZipArchive zip, string entryName, byte[] content, CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var target = entry.Open();
        await target.WriteAsync(content, ct);
    }

    /// <summary>Email không chứa dấu phân cách thư mục, nhưng vẫn chặn để tên entry không bị lạc chỗ.</summary>
    private static string SafeFolderName(string email) =>
        email.Replace('/', '_').Replace('\\', '_');

    private static string DomainLabel(ScriptDomain domain) => domain switch
    {
        ScriptDomain.ItTechnology => "IT/Technology",
        ScriptDomain.Education => "Education",
        _ => "Daily Life"
    };

    /// <summary>
    /// Chép một file audio từ kho vào ZIP. Lỗi một file thì bỏ file đó, vẫn giữ dòng metadata
    /// để dataset không hỏng hoàn toàn vì một object thiếu.
    /// </summary>
    private async Task<string?> TryAddAudioAsync(
        ZipArchive zip, string recordingId, string key, CancellationToken ct)
    {
        var ext = Path.GetExtension(key);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".wav";

        var entryName = $"audio/{recordingId}{ext}";

        try
        {
            // Đọc xong mới tạo entry: lỗi giữa chừng sẽ không để lại file cụt trong ZIP.
            await using var source = await storage.OpenReadAsync(key, ct);
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, ct);

            var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
            await using var target = entry.Open();
            buffer.Position = 0;
            await buffer.CopyToAsync(target, ct);

            return entryName;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Không đóng gói được audio {RecordingId} ({Key}) vào dataset; bỏ file, giữ metadata.",
                recordingId, key);
            return null;
        }
    }

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
