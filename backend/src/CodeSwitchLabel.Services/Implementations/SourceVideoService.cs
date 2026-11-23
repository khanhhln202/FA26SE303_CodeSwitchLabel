using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Services.Implementations;

public class SourceVideoService(
    CodeSwitchLabelDbContext db,
    IObjectStorage storage,
    IAudioProcessor audio,
    ITimedAsrClient asr,
    IOptions<SourceCorpusOptions> options,
    TimeProvider clock,
    ILogger<SourceVideoService> logger) : ISourceVideoService
{
    private SourceCorpusOptions Options => options.Value;

    public async Task<SourceVideoDto> SubmitAsync(
        SubmitSourceVideoRequest request, long userId, CancellationToken ct = default)
    {
        if (!SourceCorpusRules.TryParseYouTubeUrl(
                request.YoutubeUrl, out var videoId, out var canonicalUrl))
        {
            throw new UnprocessableException(
                "invalid_youtube_url",
                "Chỉ nhận link một video YouTube (watch, youtu.be, shorts), không nhận playlist.");
        }

        var existing = await db.SourceVideos.AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.YoutubeUrl == canonicalUrl || v.YoutubeVideoId == videoId, ct);

        if (existing is not null)
        {
            throw new ConflictException(
                "source_video_exists",
                $"Video này đã có trong hàng đợi (#{existing.SourceVideoId}).");
        }

        var now = clock.GetUtcNow();
        var video = new SourceVideo
        {
            YoutubeUrl = canonicalUrl,
            YoutubeVideoId = videoId,
            Status = SourceVideoStatus.Queued,
            SubmittedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.SourceVideos.Add(video);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Hai người gửi cùng video đúng lúc: kiểm tra phía trên đã lọt, unique index chặn.
            throw new ConflictException(
                "source_video_exists", "Video này đã có trong hàng đợi.");
        }

        return await ToDtoAsync(video, ct);
    }

    public async Task<SourceVideoDto> GetAsync(
        long sourceVideoId, long userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await db.SourceVideos.AsNoTracking()
            .FirstOrDefaultAsync(v => v.SourceVideoId == sourceVideoId, ct)
            ?? throw new NotFoundException(
                "source_video_not_found", $"Không tìm thấy video nguồn #{sourceVideoId}.");

        if (!isAdmin && video.SubmittedBy != userId)
        {
            throw new ForbiddenException(
                "source_video_forbidden", "Bạn chỉ xem được video do mình gửi.");
        }

        return await ToDtoAsync(video, ct);
    }

    public async Task<PagedResult<SourceVideoListItem>> ListAsync(
        PageRequest page, SourceVideoStatus? status, CancellationToken ct = default)
    {
        var query = db.SourceVideos.AsNoTracking();

        if (status is { } wanted) query = query.Where(v => v.Status == wanted);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.SourceVideoId)
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(v => new SourceVideoListItem(
                v.SourceVideoId,
                v.YoutubeUrl,
                v.Title,
                v.DurationSec,
                v.Status,
                v.ErrorMessage,
                v.Attempts,
                v.Segments.Count,
                v.Submitter.FullName,
                v.CreatedAt,
                v.ApprovedAt))
            .ToListAsync(ct);

        return new PagedResult<SourceVideoListItem>(items, page.Page, page.PageSize, total);
    }

    public async Task<SourceVideoDto?> ClaimNextAsync(
        long reviewerId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var ttl = TimeSpan.FromMinutes(Math.Max(1, Options.ClaimMinutes));

        // Transaction của riêng mình khi chạy thật; trong test tích hợp đã có transaction
        // bao ngoài thì dùng luôn, vì EF không mở transaction lồng nhau.
        var tx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            // Khóa hàng rồi mới đổi trạng thái: hai Reviewer bấm cùng lúc thì người sau
            // nhận video kế tiếp, không bao giờ cùng một video.
            // ToListAsync chứ không FirstOrDefaultAsync: câu SQL đã có LIMIT 1, để EF ghép thêm
            // LIMIT lần nữa quanh một câu có FOR UPDATE là chỗ dễ sinh SQL không hợp lệ.
            var rows = await db.SourceVideos.FromSqlInterpolated($"""
                SELECT * FROM source_video
                WHERE (status = 'ready_for_review'
                       OR (status = 'in_review' AND claim_expires_at <= {now}))
                  AND NOT EXISTS (
                      SELECT 1 FROM source_video held
                      WHERE held.claimed_by = {reviewerId}
                        AND held.status = 'in_review'
                        AND held.claim_expires_at > {now})
                ORDER BY created_at, source_video_id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """).ToListAsync(ct);

            var video = rows.FirstOrDefault();

            if (video is null)
            {
                if (tx is not null) await tx.CommitAsync(ct);
                return null;
            }

            video.Status = SourceVideoStatus.InReview;
            video.ClaimedBy = reviewerId;
            video.ClaimExpiresAt = now.Add(ttl);
            video.UpdatedAt = now;

            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);

            return await ToDtoAsync(video, ct);
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    public async Task<SourceVideoDto> SaveSegmentsAsync(
        long sourceVideoId, long reviewerId, SaveSourceSegmentsRequest request,
        CancellationToken ct = default)
    {
        var video = await LoadClaimedAsync(sourceVideoId, reviewerId, ct);

        SourceCorpusRules.ValidateSegments(request.Segments);

        await db.SourceSegments
            .Where(s => s.SourceVideoId == sourceVideoId)
            .ExecuteDeleteAsync(ct);

        for (var i = 0; i < request.Segments.Count; i++)
        {
            var input = request.Segments[i];
            var position = i + 1;

            db.SourceSegments.Add(new SourceSegment
            {
                SourceVideoId = sourceVideoId,
                Position = position,
                StartSec = Math.Round(input.StartSec, 3),
                EndSec = Math.Round(input.EndSec, 3),
                Text = input.Text.Trim(),
                Language = SourceCorpusRules.NormalizeLanguage(input.Language, position)
            });
        }

        video.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return await ToDtoAsync(video, ct);
    }

    public async Task<SourceVideoDto> ApproveAsync(
        long sourceVideoId, long reviewerId, CancellationToken ct = default)
    {
        var video = await LoadClaimedAsync(sourceVideoId, reviewerId, ct);

        var segments = await db.SourceSegments
            .Where(s => s.SourceVideoId == sourceVideoId)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);

        if (segments.Count == 0)
        {
            throw new ConflictException(
                "source_segments_empty", "Chưa có câu nào nên không duyệt được.");
        }

        var unlabeled = segments.FirstOrDefault(s => s.Language is not ("vi" or "en"));
        if (unlabeled is not null)
        {
            throw new UnprocessableException(
                "source_segment_language_required",
                $"Câu {unlabeled.Position} chưa có nhãn vi/en.");
        }

        if (video.AudioKey is null)
        {
            throw new ConflictException(
                "source_audio_missing", "Video chưa có file âm thanh nguồn để cắt clip.");
        }

        var tempDir = Directory.CreateTempSubdirectory("csl-clips-");

        try
        {
            var sourcePath = Path.Combine(tempDir.FullName, "source.wav");

            await using (var source = await storage.OpenReadAsync(video.AudioKey, ct))
            await using (var target = File.Create(sourcePath))
            {
                await source.CopyToAsync(target, ct);
            }

            foreach (var segment in segments)
            {
                var clipPath = Path.Combine(
                    tempDir.FullName, $"clip-{segment.Position.ToString("D4", CultureInfo.InvariantCulture)}.wav");

                await audio.CutAsync(sourcePath, clipPath, segment.StartSec, segment.EndSec, ct);

                var key = $"source-clips/{sourceVideoId}/{segment.Position.ToString("D4", CultureInfo.InvariantCulture)}.wav";
                await storage.UploadAsync(key, clipPath, "audio/wav", ct);
                segment.ClipKey = key;
            }

            video.Status = SourceVideoStatus.Approved;
            video.ApprovedAt = clock.GetUtcNow();
            video.ClaimedBy = null;
            video.ClaimExpiresAt = null;
            video.UpdatedAt = clock.GetUtcNow();

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            TryDelete(tempDir);
        }

        return await ToDtoAsync(video, ct);
    }

    public async Task RetryAsync(long sourceVideoId, CancellationToken ct = default)
    {
        var video = await db.SourceVideos
            .FirstOrDefaultAsync(v => v.SourceVideoId == sourceVideoId, ct)
            ?? throw new NotFoundException(
                "source_video_not_found", $"Không tìm thấy video nguồn #{sourceVideoId}.");

        if (video.Status != SourceVideoStatus.Failed)
        {
            throw new ConflictException(
                "source_video_not_failed", "Chỉ thử lại được video đang ở trạng thái Failed.");
        }

        video.Status = SourceVideoStatus.Queued;
        video.ErrorMessage = null;
        video.Attempts = 0;
        video.UpdatedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(ct);
    }

    public async Task<DatasetFileDto> ExportAsync(CancellationToken ct = default)
    {
        var videos = await db.SourceVideos.AsNoTracking()
            .Where(v => v.Status == SourceVideoStatus.Approved)
            .OrderBy(v => v.SourceVideoId)
            .Select(v => new
            {
                v.SourceVideoId,
                v.YoutubeUrl,
                v.Title,
                v.DurationSec,
                v.YoutubeVideoId,
                v.AudioKey,
                SubmitterName = v.Submitter.FullName,
                v.CreatedAt
            })
            .ToListAsync(ct);

        var ids = videos.Select(v => v.SourceVideoId).ToList();

        var segments = ids.Count == 0
            ? new List<SourceSegment>()
            : await db.SourceSegments.AsNoTracking()
                .Where(s => ids.Contains(s.SourceVideoId))
                .OrderBy(s => s.SourceVideoId)
                .ThenBy(s => s.Position)
                .ToListAsync(ct);

        var stream = new FileStream(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024,
            FileOptions.DeleteOnClose);

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var video in videos)
            {
                var folder = $"{SanitizeFileNamePart(video.SubmitterName)}/{video.SourceVideoId:D3}";
                var stem = $"video{video.SourceVideoId.ToString(CultureInfo.InvariantCulture)}";
                var videoSegments = segments.Where(s => s.SourceVideoId == video.SourceVideoId).ToList();
                var entries = videoSegments.Select(s => new
                {
                    start = decimal.ToInt64(decimal.Round(s.StartSec * 1000, 0)),
                    end = decimal.ToInt64(decimal.Round(s.EndSec * 1000, 0)),
                    text = s.Text,
                    language = s.Language
                }).ToList();
                var json = JsonSerializer.Serialize(new
                {
                    video_id = video.YoutubeVideoId,
                    language = videoSegments.Select(s => s.Language).Distinct().Count() == 1
                        ? videoSegments.FirstOrDefault()?.Language : null,
                    segments = entries
                }, new JsonSerializerOptions { WriteIndented = true });
                await WriteZipTextAsync(zip, $"{folder}/{stem}.json", json, ct);
                await WriteZipTextAsync(zip, $"{folder}/{stem}.tsv",
                    "start\tend\ttext\n" + string.Join("\n", videoSegments.Select(s =>
                        $"{(s.StartSec * 1000).ToString(CultureInfo.InvariantCulture)}\t{(s.EndSec * 1000).ToString(CultureInfo.InvariantCulture)}\t{s.Text}")), ct);
                await WriteZipTextAsync(zip, $"{folder}/{stem}.srt", BuildSubtitle(videoSegments, vtt: false), ct);
                await WriteZipTextAsync(zip, $"{folder}/{stem}.vtt", BuildSubtitle(videoSegments, vtt: true), ct);
                await WriteZipTextAsync(zip, $"{folder}/{stem}.txt",
                    string.Join(" ", videoSegments.Select(s => s.Text)), ct);

                if (video.AudioKey is not null)
                {
                    await using var source = await storage.OpenReadAsync(video.AudioKey, ct);
                    var entry = zip.CreateEntry($"{folder}/{stem}.wav", CompressionLevel.NoCompression);
                    await using var target = entry.Open();
                    await source.CopyToAsync(target, ct);
                }
            }
        }

        stream.Position = 0;
        return new DatasetFileDto("source-corpus.zip", "application/zip", stream);
    }

    private static async Task WriteZipTextAsync(
        ZipArchive zip, string path, string content, CancellationToken ct)
    {
        var entry = zip.CreateEntry(path);
        await using var output = entry.Open();
        await using var writer = new StreamWriter(output, new UTF8Encoding(false));
        await writer.WriteAsync(content.AsMemory(), ct);
    }

    private static string BuildSubtitle(IReadOnlyList<SourceSegment> segments, bool vtt)
    {
        var lines = new List<string>();
        if (vtt) lines.Add("WEBVTT");
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (!vtt) lines.Add((i + 1).ToString(CultureInfo.InvariantCulture));
            lines.Add($"{FormatTimestamp(segment.StartSec, vtt)} --> {FormatTimestamp(segment.EndSec, vtt)}");
            lines.Add(segment.Text);
            lines.Add(string.Empty);
        }
        return string.Join("\n", lines);
    }

    private static string FormatTimestamp(decimal seconds, bool vtt)
    {
        var milliseconds = decimal.ToInt64(decimal.Round(seconds * 1000, 0));
        var hours = milliseconds / 3_600_000;
        var minutes = milliseconds / 60_000 % 60;
        var remainder = milliseconds % 60_000;
        var separator = vtt ? '.' : ',';
        return $"{hours:00}:{minutes:00}:{remainder / 1000:00}{separator}{remainder % 1000:000}";
    }

    private static string SanitizeFileNamePart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "unknown" : safe;
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        var video = await ClaimForProcessingAsync(ct);
        if (video is null) return false;

        await ProcessAsync(video, ct);
        return true;
    }

    private async Task<SourceVideo?> ClaimForProcessingAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // Job "processing" cũ hơn trần xử lý là dấu hiệu API đã chết giữa chừng;
        // nhận lại để không kẹt vĩnh viễn. Chỉ một instance API chạy nên không sợ trùng.
        var staleBefore = now.AddSeconds(-(Options.ProcessTimeoutSeconds + 120));

        var tx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            var rows = await db.SourceVideos.FromSqlInterpolated($"""
                SELECT * FROM source_video
                WHERE status = 'queued'
                   OR (status = 'processing' AND updated_at < {staleBefore})
                ORDER BY created_at, source_video_id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """).ToListAsync(ct);

            var video = rows.FirstOrDefault();

            if (video is null)
            {
                if (tx is not null) await tx.CommitAsync(ct);
                return null;
            }

            video.Status = SourceVideoStatus.Processing;
            video.Attempts += 1;
            video.ErrorMessage = null;
            video.UpdatedAt = now;

            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);

            return video;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    private async Task ProcessAsync(SourceVideo video, CancellationToken ct)
    {
        var tempDir = Directory.CreateTempSubdirectory("csl-source-");

        try
        {
            var inputPath = await DownloadAsync(video, tempDir.FullName, ct);
            var wavPath = Path.Combine(tempDir.FullName, "audio.wav");

            await audio.ConvertToWavAsync(
                inputPath, wavPath, ct, Options.ProcessTimeoutSeconds);

            var probe = await audio.ProbeAsync(wavPath, ct);
            video.DurationSec = probe.DurationSec;

            var transcript = await asr.TranscribeFileAsync(wavPath, ct);
            var segments = BuildSegments(transcript, probe.DurationSec);

            if (segments.Count == 0)
            {
                throw new UnprocessableException(
                    "source_transcript_empty",
                    "Dịch vụ nhận dạng không trả về câu nào cho video này.");
            }

            var key = $"source-audio/{video.SourceVideoId}.wav";
            await storage.UploadAsync(key, wavPath, "audio/wav", ct);

            video.AudioKey = key;
            video.Transcript = transcript.Text;
            video.Status = SourceVideoStatus.ReadyForReview;
            video.ErrorMessage = null;
            video.UpdatedAt = clock.GetUtcNow();

            await db.SourceSegments
                .Where(s => s.SourceVideoId == video.SourceVideoId)
                .ExecuteDeleteAsync(ct);

            foreach (var segment in segments)
            {
                segment.SourceVideoId = video.SourceVideoId;
                db.SourceSegments.Add(segment);
            }

            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // API đang tắt: trả job về hàng đợi để lần khởi động sau làm tiếp.
            await MarkFailedOrRequeueAsync(
                video, "Tiến trình nền dừng giữa chừng; job sẽ chạy lại.", CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Xử lý video nguồn #{SourceVideoId} hỏng (lần {Attempts}).",
                video.SourceVideoId, video.Attempts);

            await MarkFailedOrRequeueAsync(video, ex.Message, CancellationToken.None);
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    private async Task MarkFailedOrRequeueAsync(
        SourceVideo video, string error, CancellationToken ct)
    {
        // Lần lưu thành công thất bại có thể còn câu mới trong change tracker;
        // bỏ chúng đi để không ghi dữ liệu dở dang kèm trạng thái lỗi.
        foreach (var entry in db.ChangeTracker.Entries<SourceSegment>()
                     .Where(e => e.State == EntityState.Added).ToList())
        {
            entry.State = EntityState.Detached;
        }

        video.Status = video.Attempts >= Math.Max(1, Options.MaxAttempts)
            ? SourceVideoStatus.Failed
            : SourceVideoStatus.Queued;
        video.ErrorMessage = Truncate(error, 2000);
        video.UpdatedAt = clock.GetUtcNow();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không ghi được trạng thái lỗi cho video #{SourceVideoId}.", video.SourceVideoId);
        }
    }

    private async Task<string> DownloadAsync(
        SourceVideo video, string directory, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Options.YtDlpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directory
        };

        void Add(string value) => startInfo.ArgumentList.Add(value);

        Add("--no-playlist");
        Add("--no-progress");
        Add("--newline");
        Add("--print-json");
        Add("-f");
        Add("bestaudio/best");
        Add("-o");
        Add(Path.Combine(directory, "audio.%(ext)s"));

        if (!string.IsNullOrWhiteSpace(Options.JsRuntimes))
        {
            Add("--js-runtimes");
            Add(Options.JsRuntimes);
        }

        // Thiếu file cookie thì bỏ qua: máy chủ chưa cấu hình vẫn tải như trước.
        // yt-dlp ghi đè file cookie khi thoát, nên chép sang thư mục tạm: file nguồn
        // được mount read-only và container không sửa được cookie trên máy chủ.
        if (!string.IsNullOrWhiteSpace(Options.CookiesPath)
            && File.Exists(Options.CookiesPath))
        {
            var cookies = Path.Combine(directory, "cookies.txt");
            File.Copy(Options.CookiesPath, cookies, overwrite: true);
            Add("--cookies");
            Add(cookies);
        }

        Add(video.YoutubeUrl);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new UnprocessableException(
                    "source_download_failed", "Không khởi động được yt-dlp.");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Không tìm thấy '{Options.YtDlpPath}'. Cài yt-dlp hoặc đặt SourceCorpus:YtDlpPath.", ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, Options.ProcessTimeoutSeconds)));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);

            if (ct.IsCancellationRequested) throw;

            throw new UnprocessableException(
                "source_download_timeout",
                $"Tải video quá {Options.ProcessTimeoutSeconds} giây nên đã bị dừng.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            logger.LogWarning("yt-dlp thoát với mã {Code}: {Error}", process.ExitCode, stderr.Trim());
            throw DownloadError(stderr);
        }

        ReadMetadata(stdout, video);

        return Directory.GetFiles(directory, "audio.*")
            .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                        && !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        && !f.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new UnprocessableException(
                "source_download_failed", "yt-dlp chạy xong nhưng không để lại file âm thanh.");
    }

    /// <summary>
    /// Lỗi hay gặp nhất là video riêng tư / bị chặn / đã xoá. Tách mã riêng để người gửi
    /// biết sửa link thay vì bấm thử lại vô ích.
    /// </summary>
    private static UnprocessableException DownloadError(string stderr)
    {
        var lower = stderr.ToLowerInvariant();

        // IP datacenter bị YouTube chặn: nói rõ để người trực biết phải làm mới cookie,
        // không phải đi tìm link khác.
        if (lower.Contains("sign in to confirm"))
        {
            return new UnprocessableException(
                "source_video_unavailable",
                "YouTube yêu cầu đăng nhập từ máy chủ. Cookie yt-dlp thiếu hoặc đã hết hạn.");
        }

        if (lower.Contains("private video")
            || lower.Contains("video unavailable")
            || lower.Contains("not available in your country")
            || lower.Contains("this video is unavailable"))
        {
            return new UnprocessableException(
                "source_video_unavailable",
                "Video không truy cập được (riêng tư, bị chặn hoặc đã xoá).");
        }

        var lastLine = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Trim();

        return new UnprocessableException(
            "source_download_failed",
            string.IsNullOrWhiteSpace(lastLine)
                ? "yt-dlp không tải được video."
                : $"yt-dlp không tải được video: {Truncate(lastLine, 500)}");
    }

    private static void ReadMetadata(string stdout, SourceVideo video)
    {
        var line = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(l => l.TrimStart().StartsWith('{'));

        if (line is null) return;

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (root.TryGetProperty("title", out var title))
            {
                video.Title = Truncate(title.GetString() ?? string.Empty, 500);
            }

            if (root.TryGetProperty("duration", out var duration)
                && duration.ValueKind == JsonValueKind.Number
                && duration.TryGetDecimal(out var seconds))
            {
                video.DurationSec = Math.Round(seconds, 2);
            }
        }
        catch (JsonException)
        {
            // Metadata chỉ để hiển thị; thiếu nó không chặn việc phiên âm.
        }
    }

    private static List<SourceSegment> BuildSegments(AsrTranscript transcript, decimal duration)
    {
        var segments = new List<SourceSegment>();

        foreach (var item in transcript.Segments)
        {
            var start = Math.Round(Math.Max(0, item.StartSec), 3);
            var end = Math.Round(Math.Min(item.EndSec, duration), 3);

            if (end <= start) continue;

            segments.Add(new SourceSegment
            {
                Position = segments.Count + 1,
                StartSec = start,
                EndSec = end,
                Text = item.Text.Trim()
            });
        }

        // Nhà cung cấp không trả segments thì vẫn giữ được bản phiên âm;
        // Reviewer tự tách câu bằng mốc thời gian.
        if (segments.Count == 0
            && !string.IsNullOrWhiteSpace(transcript.Text)
            && duration > 0)
        {
            segments.Add(new SourceSegment
            {
                Position = 1,
                StartSec = 0,
                EndSec = duration,
                Text = transcript.Text.Trim()
            });
        }

        return segments;
    }

    private async Task<SourceVideo> LoadClaimedAsync(
        long sourceVideoId, long reviewerId, CancellationToken ct)
    {
        var video = await db.SourceVideos
            .FirstOrDefaultAsync(v => v.SourceVideoId == sourceVideoId, ct)
            ?? throw new NotFoundException(
                "source_video_not_found", $"Không tìm thấy video nguồn #{sourceVideoId}.");

        if (video.Status != SourceVideoStatus.InReview)
        {
            throw new ConflictException(
                "source_video_not_in_review",
                "Video không ở trạng thái đang duyệt.");
        }

        if (video.ClaimedBy != reviewerId
            || video.ClaimExpiresAt is null
            || video.ClaimExpiresAt <= clock.GetUtcNow())
        {
            throw new ConflictException(
                "source_video_not_claimed",
                "Bạn không giữ video này nữa — hết hạn hoặc đã có người khác nhận.");
        }

        return video;
    }

    private async Task<SourceVideoDto> ToDtoAsync(
        SourceVideo video, CancellationToken ct)
    {
        var segments = await db.SourceSegments.AsNoTracking()
            .Where(s => s.SourceVideoId == video.SourceVideoId)
            .OrderBy(s => s.Position)
            .Select(s => new SourceSegmentDto(
                s.SourceSegmentId, s.Position, s.StartSec, s.EndSec, s.Text, s.Language))
            .ToListAsync(ct);

        string? audioUrl = null;
        DateTimeOffset? expiresAt = null;

        if (video.AudioKey is not null)
        {
            try
            {
                (audioUrl, expiresAt) = await storage.GetDownloadUrlAsync(video.AudioKey, ct);
            }
            catch (Exception ex)
            {
                // Kho lưu trữ trục trặc thì vẫn trả transcript để Reviewer đọc;
                // chỉ mất nút nghe. Lỗi này không nên làm hỏng cả trang.
                logger.LogWarning(
                    ex, "Không tạo được link nghe cho video #{SourceVideoId}.", video.SourceVideoId);
            }
        }

        return new SourceVideoDto(
            video.SourceVideoId,
            video.YoutubeUrl,
            video.Title,
            video.DurationSec,
            video.Status,
            video.ErrorMessage,
            video.Attempts,
            audioUrl,
            expiresAt,
            video.Transcript,
            segments,
            video.SubmittedBy,
            video.CreatedAt,
            video.ApprovedAt);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Tiến trình vừa tự thoát — không cần làm gì.
        }
    }

    private static void TryDelete(DirectoryInfo directory)
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // File tạm bị giữ bởi tiến trình khác thì để hệ điều hành dọn.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
