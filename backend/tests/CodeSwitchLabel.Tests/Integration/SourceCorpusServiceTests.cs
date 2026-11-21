using System.IO.Compression;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Luồng corpus hội thoại: gửi link, Reviewer giữ video, chốt câu, cắt clip, xuất ZIP.
/// Chạy trên PostgreSQL thật vì phần khó nhất nằm ở câu SQL giữ hàng
/// (<c>FOR UPDATE SKIP LOCKED</c>) và ràng buộc duy nhất của lược đồ.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class SourceCorpusServiceTests : IntegrationTestBase
{
    private readonly ISourceVideoService _corpus;

    public SourceCorpusServiceTests(DatabaseFixture fixture) : base(fixture)
    {
        _corpus = Services.GetRequiredService<ISourceVideoService>();
    }

    [Fact]
    public async Task Submit_ChuanHoaLink_VaChanGuiTrung()
    {
        var ct = TestContext.Current.CancellationToken;

        var created = await _corpus.SubmitAsync(
            new SubmitSourceVideoRequest { YoutubeUrl = "https://youtu.be/dQw4w9WgXcQ?t=3" },
            SpeakerUserId, ct);

        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", created.YoutubeUrl);
        Assert.Equal(SourceVideoStatus.Queued, created.Status);

        var trung = await Assert.ThrowsAsync<ConflictException>(() => _corpus.SubmitAsync(
            new SubmitSourceVideoRequest { YoutubeUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" },
            SpeakerUserId, ct));

        Assert.Equal("source_video_exists", trung.Code);
    }

    [Fact]
    public async Task Duyet_CatClipRoiMoiXuatHienTrongManifest()
    {
        var ct = TestContext.Current.CancellationToken;
        var video = await SeedReadyVideoAsync("abcdefghijk", ct);

        var claimed = await _corpus.ClaimNextAsync(ReviewerUserId, ct);

        Assert.NotNull(claimed);
        Assert.Equal(video.SourceVideoId, claimed.SourceVideoId);
        Assert.Equal(SourceVideoStatus.InReview, claimed.Status);
        Assert.NotNull(claimed.AudioUrl);

        var saved = await _corpus.SaveSegmentsAsync(
            video.SourceVideoId, ReviewerUserId,
            new SaveSourceSegmentsRequest
            {
                Segments =
                [
                    new SourceSegmentInput
                    {
                        StartSec = 0, EndSec = 2.5m, Text = "Xin chào", Language = "vi"
                    },
                    new SourceSegmentInput
                    {
                        StartSec = 2.5m, EndSec = 6, Text = "hello", Language = "en"
                    }
                ]
            }, ct);

        Assert.Equal(2, saved.Segments.Count);

        var approved = await _corpus.ApproveAsync(video.SourceVideoId, ReviewerUserId, ct);
        Assert.Equal(SourceVideoStatus.Approved, approved.Status);

        var clips = await Db.SourceSegments.AsNoTracking()
            .Where(s => s.SourceVideoId == video.SourceVideoId)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);

        Assert.All(clips, clip => Assert.NotNull(clip.ClipKey));

        var file = await _corpus.ExportAsync(ct);
        file.Content.Position = 0;

        using var zip = new ZipArchive(file.Content, ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("manifest.jsonl"));
        Assert.NotNull(zip.GetEntry($"clips/{video.SourceVideoId}_0001.wav"));
        Assert.NotNull(zip.GetEntry($"clips/{video.SourceVideoId}_0002.wav"));

        using var reader = new StreamReader(zip.GetEntry("manifest.jsonl")!.Open());
        var lines = (await reader.ReadToEndAsync(ct))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Contains("\"language\":\"vi\"", lines[0]);
        Assert.Contains("\"language\":\"en\"", lines[1]);
        Assert.Contains(
            $"\"youtubeUrl\":\"https://www.youtube.com/watch?v=abcdefghijk\"", lines[0]);
    }

    [Fact]
    public async Task Approve_ConCauChuaGanNhan_ThiChanVaGiuNguyenTrangThai()
    {
        var ct = TestContext.Current.CancellationToken;
        var video = await SeedReadyVideoAsync("zyxwvutsrqp", ct);

        await _corpus.ClaimNextAsync(ReviewerUserId, ct);
        await _corpus.SaveSegmentsAsync(
            video.SourceVideoId, ReviewerUserId,
            new SaveSourceSegmentsRequest
            {
                Segments =
                [
                    new SourceSegmentInput { StartSec = 0, EndSec = 2, Text = "Xin chào" }
                ]
            }, ct);

        var loi = await Assert.ThrowsAsync<UnprocessableException>(
            () => _corpus.ApproveAsync(video.SourceVideoId, ReviewerUserId, ct));

        Assert.Equal("source_segment_language_required", loi.Code);

        var status = await Db.SourceVideos.AsNoTracking()
            .Where(v => v.SourceVideoId == video.SourceVideoId)
            .Select(v => v.Status)
            .SingleAsync(ct);

        Assert.Equal(SourceVideoStatus.InReview, status);
    }

    private async Task<SourceVideo> SeedReadyVideoAsync(string videoId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var video = new SourceVideo
        {
            YoutubeUrl = $"https://www.youtube.com/watch?v={videoId}",
            YoutubeVideoId = videoId,
            Title = "Hội thoại thử",
            DurationSec = 6,
            AudioKey = "source-audio/test.wav",
            Transcript = "Xin chào hello",
            Status = SourceVideoStatus.ReadyForReview,
            SubmittedBy = SpeakerUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        Db.SourceVideos.Add(video);
        await Db.SaveChangesAsync(ct);
        return video;
    }
}
