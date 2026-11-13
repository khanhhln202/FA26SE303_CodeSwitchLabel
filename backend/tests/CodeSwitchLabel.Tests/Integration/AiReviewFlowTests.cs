using System.Text;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Implementations;
using CodeSwitchLabel.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeSwitchLabel.Tests.Integration;

/// <summary>
/// Máy duyệt chạy trên DATABASE THẬT: repository thật, ReviewService thật, và trigger
/// trg_review_majority thật. Chỉ ASR và kho file là giả, vì gọi ASR thật thì tốn tiền
/// và cần khoá API.
///
/// Đây là chỗ kiểm thứ mà test đơn vị không kiểm được: phiếu của máy có thật sự đi vào
/// bảng review đúng vòng không, và trigger có chốt bản ghi sau phiếu đó không.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class AiReviewFlowTests : IntegrationTestBase
{
    /// <summary>
    /// Lược đồ có unique index uq_script_cs_content_md5, nên mỗi bài test phải dùng một câu
    /// khác nhau — cả bộ test dùng chung một database.
    /// </summary>
    private static int _seq;

    private const string CsPattern = "[vi]Mai [en]deploy [vi]bản mới số {0} nhé.";
    private const string ViPattern = "[vi]Mai triển khai bản mới số {0} nhé.";

    public AiReviewFlowTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    // ---------------------------------------------------------------- giàn

    private StubAsr _asr = new("");
    private string _expectedText = string.Empty;
    private string _transcriptDung = string.Empty;

    private AiReviewService ServiceWith(string transcript)
    {
        _asr = new StubAsr(transcript);

        return new AiReviewService(
            Services.GetRequiredService<IAiReviewRepository>(),
            ReviewService,
            new StubStorage(),
            _asr,
            SystemConfigService,
            Options.Create(new AudioOptions()),
            TimeProvider.System,
            NullLogger<AiReviewService>.Instance);
    }

    private async Task EnableAsync(CancellationToken ct) =>
        await SystemConfigService.UpdateAsync(ConfigKeys.AiReviewEnabled, "true", AdminUserId, ct);

    /// <summary>Một bản ghi đang chờ duyệt, chưa ai chấm.</summary>
    private async Task<Recording> PendingRecordingAsync()
    {
        var n = Interlocked.Increment(ref _seq);

        var script = await CreateValidatedScriptAsync(
            string.Format(CsPattern, n), string.Format(ViPattern, n),
            "ItTechnology", AdminUserId);

        _expectedText = $"mai deploy bản mới số {n} nhé";
        _transcriptDung = $"Mai deploy bản mới số {n} nhé.";

        return await CreateRecordingAsync(
            script.ScriptId, SpeakerUserId, SentenceVariant.CodeSwitching);
    }

    /// <summary>Ghi thẳng phiếu của người vào database — trigger vẫn chạy như thật.</summary>
    private async Task AddHumanVoteAsync(
        Recording recording, long reviewerId, short round, ReviewDecision decision, CancellationToken ct)
    {
        Db.Reviews.Add(new TestDataBuilders.ReviewBuilder()
            .WithRecordingId(recording.RecordingId)
            .WithReviewerId(reviewerId)
            .WithRound(round)
            .WithDecision(decision)
            .Build());

        await Db.SaveChangesAsync(ct);
        await Db.Entry(recording).ReloadAsync(ct);
    }

    private async Task<long> SecondReviewerAsync() =>
        (await CreateUserWithBuilderAsync(RoleName.Reviewer, fullName: "Người duyệt phá thế")).UserId;

    // ---------------------------------------------------------------- bài test

    [Fact]
    public async Task HaiNguoiLechNhau_DocDung_MayDuyetDat_VaTriggerChotBanGhi()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnableAsync(ct);

        var recording = await PendingRecordingAsync();
        var other = await SecondReviewerAsync();

        await AddHumanVoteAsync(recording, ReviewerUserId, 1, ReviewDecision.Approved, ct);
        await AddHumanVoteAsync(recording, other, 2, ReviewDecision.Rejected, ct);

        // Hai người lệch nhau thì bản ghi phải còn chờ — chưa bên nào quá bán.
        Assert.Equal(RecordingStatus.PendingReview, recording.Status);

        var result = await ServiceWith(_transcriptDung)
            .AnalyzeAsync(recording.RecordingId, ct);

        Assert.Equal(AiReviewVerdicts.Approved, result.Verdict);
        Assert.Equal(0m, result.EffectiveWer);

        // Phiếu của máy phải là lượt THỨ BA trong bảng review thật.
        var votes = await Db.Reviews
            .Where(v => v.RecordingId == recording.RecordingId)
            .OrderBy(v => v.ReviewRound)
            .ToListAsync(ct);

        Assert.Equal(3, votes.Count);
        Assert.Equal(3, votes[2].ReviewRound);
        Assert.Equal(ReviewDecision.Approved, votes[2].Decision);

        var ai = await Db.AppUsers.FirstAsync(u => u.Email == AiReviewer.Email, ct);
        Assert.Equal(ai.UserId, votes[2].ReviewerId);

        // Và trigger phải chốt bản ghi ngay sau phiếu thứ ba.
        await Db.Entry(recording).ReloadAsync(ct);
        Assert.Equal(RecordingStatus.Approved, recording.Status);
    }

    [Fact]
    public async Task HaiNguoiLechNhau_DocSai_MayTuChoi_VaBanGhiBiLoai()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnableAsync(ct);

        var recording = await PendingRecordingAsync();
        var other = await SecondReviewerAsync();

        await AddHumanVoteAsync(recording, ReviewerUserId, 1, ReviewDecision.Approved, ct);
        await AddHumanVoteAsync(recording, other, 2, ReviewDecision.Rejected, ct);

        var result = await ServiceWith("Hôm nay trời mưa rất to.")
            .AnalyzeAsync(recording.RecordingId, ct);

        Assert.Equal(AiReviewVerdicts.Rejected, result.Verdict);

        // Từ chối thì phải kèm lý do, và máy chỉ được nêu content_mismatch.
        var vote = await Db.Reviews
            .Include(v => v.RejectionReasons).ThenInclude(rr => rr.Reason)
            .Where(v => v.RecordingId == recording.RecordingId && v.ReviewRound == 3)
            .SingleAsync(ct);

        Assert.Equal(ReviewDecision.Rejected, vote.Decision);
        var reason = Assert.Single(vote.RejectionReasons);
        Assert.Equal(AiReviewEvaluator.ContentMismatch, reason.Reason.ReasonCode);

        await Db.Entry(recording).ReloadAsync(ct);
        Assert.Equal(RecordingStatus.Rejected, recording.Status);
    }

    [Fact]
    public async Task BangChung_DuocGhi_VaTroToiDungLuotDuyet()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnableAsync(ct);

        var recording = await PendingRecordingAsync();
        var other = await SecondReviewerAsync();

        await AddHumanVoteAsync(recording, ReviewerUserId, 1, ReviewDecision.Rejected, ct);
        await AddHumanVoteAsync(recording, other, 2, ReviewDecision.Approved, ct);

        await ServiceWith(_transcriptDung).AnalyzeAsync(recording.RecordingId, ct);

        var evidence = await Db.AiReviews
            .SingleAsync(a => a.RecordingId == recording.RecordingId, ct);

        Assert.Equal(AiReviewVerdicts.Approved, evidence.Verdict);
        Assert.Equal(_transcriptDung, evidence.Transcript);
        Assert.Equal(_expectedText, evidence.ExpectedText);
        Assert.Equal(0m, evidence.Wer);
        Assert.NotNull(evidence.Details);

        // review_id là NOT NULL dưới lược đồ; phải trỏ đúng lượt duyệt vừa ghi.
        var round3 = await Db.Reviews
            .SingleAsync(v => v.RecordingId == recording.RecordingId && v.ReviewRound == 3, ct);

        Assert.Equal(round3.ReviewId, evidence.ReviewId);
    }

    [Theory]
    [InlineData(0)] // chưa ai duyệt
    [InlineData(1)] // mới một người
    public async Task ChuaDuHaiLuotNguoi_MayBiChan_VaKhongGoiAsr(int soLuot)
    {
        var ct = TestContext.Current.CancellationToken;
        await EnableAsync(ct);

        var recording = await PendingRecordingAsync();

        if (soLuot == 1)
        {
            await AddHumanVoteAsync(recording, ReviewerUserId, 1, ReviewDecision.Approved, ct);
        }

        var service = ServiceWith(_transcriptDung);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => service.AnalyzeAsync(recording.RecordingId, ct));

        Assert.Equal("ai_review_not_tiebreak", ex.Code);

        // Quan trọng nhất: không tiêu một lượt gọi ASR nào cho bản chưa tới lượt.
        Assert.Equal(0, _asr.Calls);

        Assert.False(
            await Db.AiReviews.AnyAsync(a => a.RecordingId == recording.RecordingId, ct),
            "bị chặn thì không được ghi dòng bằng chứng nào");
    }

    [Fact]
    public async Task HaiNguoiCungKetQua_BanGhiDaChot_MayKhongConViec()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnableAsync(ct);

        var recording = await PendingRecordingAsync();
        var other = await SecondReviewerAsync();

        await AddHumanVoteAsync(recording, ReviewerUserId, 1, ReviewDecision.Approved, ct);
        await AddHumanVoteAsync(recording, other, 2, ReviewDecision.Approved, ct);

        // Trigger đã chốt ở lượt 2 — máy không được chen vào nữa.
        Assert.Equal(RecordingStatus.Approved, recording.Status);

        var service = ServiceWith(_transcriptDung);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => service.AnalyzeAsync(recording.RecordingId, ct));

        Assert.Equal("recording_not_reviewable", ex.Code);
        Assert.Equal(0, _asr.Calls);
    }

    [Fact]
    public async Task TinhNangTat_ThiKhongChayDuDangOTheLech()
    {
        var ct = TestContext.Current.CancellationToken;
        await SystemConfigService.UpdateAsync(ConfigKeys.AiReviewEnabled, "false", AdminUserId, ct);

        var recording = await PendingRecordingAsync();
        var other = await SecondReviewerAsync();

        await AddHumanVoteAsync(recording, ReviewerUserId, 1, ReviewDecision.Approved, ct);
        await AddHumanVoteAsync(recording, other, 2, ReviewDecision.Rejected, ct);

        var service = ServiceWith(_transcriptDung);

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => service.AnalyzeAsync(recording.RecordingId, ct));

        Assert.Equal("ai_review_disabled", ex.Code);
        Assert.Equal(0, _asr.Calls);
    }

    [Fact]
    public async Task ChayLo_ChiLayDungBanDangLech()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnableAsync(ct);

        var other = await SecondReviewerAsync();

        var lech = await PendingRecordingAsync();
        var transcriptCuaBanLech = _transcriptDung;
        await AddHumanVoteAsync(lech, ReviewerUserId, 1, ReviewDecision.Approved, ct);
        await AddHumanVoteAsync(lech, other, 2, ReviewDecision.Rejected, ct);

        var chuaAiCham = await PendingRecordingAsync();

        var batch = await ServiceWith(transcriptCuaBanLech).RunBatchAsync(100, ct);

        var ids = batch.Items.Select(i => i.RecordingId).ToHashSet();

        Assert.Contains(lech.RecordingId, ids);
        Assert.DoesNotContain(chuaAiCham.RecordingId, ids);
    }

    // ---------------------------------------------------------------- giả lập

    /// <summary>
    /// Bất biến quan trọng nhất của endpoint danh sách: nó phải mô tả ĐÚNG tập mà lượt chạy
    /// lô sẽ xử lý. Lệch nhau thì giao diện hiện ra bản ghi mà bấm vào sẽ nhận 409, hoặc
    /// giấu mất bản ghi đang chờ — cả hai đều rất khó lần ra nguyên nhân.
    /// </summary>
    [Fact]
    public async Task DanhSachChoMay_MoTaDungTapMaLuotChayLoSeXuLy()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnableAsync(ct);

        var choMay = await PendingRecordingAsync();          // 2 lượt, lệch nhau  -> phải có
        var chuaAiCham = await PendingRecordingAsync();      // 0 lượt             -> không có
        var daChot = await PendingRecordingAsync();          // 2 lượt, cùng đạt   -> không có

        var nguoiHai = await SecondReviewerAsync();

        await AddHumanVoteAsync(choMay, ReviewerUserId, 1, ReviewDecision.Approved, ct);
        await AddHumanVoteAsync(choMay, nguoiHai, 2, ReviewDecision.Rejected, ct);

        await AddHumanVoteAsync(daChot, ReviewerUserId, 1, ReviewDecision.Approved, ct);
        await AddHumanVoteAsync(daChot, nguoiHai, 2, ReviewDecision.Approved, ct);

        var repo = Services.GetRequiredService<IAiReviewRepository>();
        var mayId = await repo.GetAiReviewerIdAsync(ct);
        Assert.NotNull(mayId);

        var idsLo = await repo.GetTiedRecordingIdsAsync(500, mayId.Value, ct);
        var danhSach = await repo.GetPendingAsync(500, mayId.Value, ct);
        var idsDanhSach = danhSach.Select(x => x.RecordingId).ToList();

        foreach (var (banGhi, phaiCo) in new[]
                 {
                     (choMay.RecordingId, true),
                     (chuaAiCham.RecordingId, false),
                     (daChot.RecordingId, false)
                 })
        {
            Assert.Equal(phaiCo, idsLo.Contains(banGhi));
            Assert.Equal(phaiCo, idsDanhSach.Contains(banGhi));
        }
    }

    [Fact]
    public async Task DanhSachChoMay_KemVanBanDaBoNhanVaHaiLuotCuaNguoi()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnableAsync(ct);

        var banGhi = await PendingRecordingAsync();
        var nguoiHai = await SecondReviewerAsync();
        await AddHumanVoteAsync(banGhi, ReviewerUserId, 1, ReviewDecision.Approved, ct);
        await AddHumanVoteAsync(banGhi, nguoiHai, 2, ReviewDecision.Rejected, ct);

        var dong = Assert.Single(
            await ServiceWith("").GetPendingAsync(500, ct),
            x => x.RecordingId == banGhi.RecordingId);

        // Nhãn ngôn ngữ là thứ của database, không phải thứ để hiện lên màn hình.
        Assert.DoesNotContain("[vi]", dong.ExpectedText);
        Assert.DoesNotContain("[en]", dong.ExpectedText);
        Assert.Contains("deploy", dong.ExpectedText);
        Assert.Equal(SentenceVariant.CodeSwitching, dong.Variant);

        // Hai lượt của người, đúng thứ tự vòng, đúng kết quả lệch nhau.
        Assert.Equal(2, dong.Reviews.Count);
        Assert.Equal([(short)1, (short)2], dong.Reviews.Select(v => v.Round));
        Assert.Equal(ReviewDecision.Approved, dong.Reviews[0].Decision);
        Assert.Equal(ReviewDecision.Rejected, dong.Reviews[1].Decision);
    }

    private sealed class StubAsr(string text) : IAsrClient
    {
        public int Calls { get; private set; }

        public bool IsConfigured => true;

        public Task<AsrResult> TranscribeAsync(byte[] audio, string fileName, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new AsrResult(text, "whisper-test"));
        }
    }

    private sealed class StubStorage : IObjectStorage
    {
        public Task EnsureBucketAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task UploadAsync(string key, string filePath, string contentType, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

        public string GetObjectUrl(string key) => $"s3://recordings/{key}";

        public string? GetObjectKey(string url) => url[(url.LastIndexOf('/') + 1)..];

        public Task<(string Url, DateTimeOffset ExpiresAt)> GetDownloadUrlAsync(
            string key, CancellationToken ct = default) =>
            Task.FromResult(($"s3://recordings/{key}", DateTimeOffset.UtcNow.AddMinutes(15)));

        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("RIFF....WAVEfmt ")));
    }
}
