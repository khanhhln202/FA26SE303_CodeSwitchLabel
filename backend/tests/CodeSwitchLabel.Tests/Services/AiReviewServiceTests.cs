using System.Text;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Implementations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Máy duyệt chỉ vào khi hai Reviewer người LỆCH nhau, và khi vào thì luôn phải quyết.
/// Phần lớn bài test ở đây canh đúng cửa chặn đó, vì mỗi lượt gọi ASR là một lần tốn tiền
/// và một phiếu sai chỗ sẽ chốt bản ghi sớm.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AiReviewServiceTests
{
    private const string RecordingId = "r_cs_211000000";
    private const string Expected = "[vi]Em nên [en]scan [vi]tài liệu này.";
    private const string CloudLink = "http://minio:9000/recordings/r_cs_211000000.wav";

    // ---------------------------------------------------------------- cửa chặn trước ASR

    [Fact]
    public async Task TinhNangDangTat_ThiBaoLoiVaKhongGoiAsr()
    {
        var harness = new Harness { Enabled = false };

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => harness.Service.AnalyzeAsync(RecordingId, TestContext.Current.CancellationToken));

        Assert.Equal("ai_review_disabled", ex.Code);
        Assert.Equal(0, harness.Asr.Calls);
    }

    [Fact]
    public async Task ChuaCauHinhAsr_ThiBaoLoi()
    {
        var harness = new Harness();
        harness.Asr.Configured = false;

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => harness.Service.AnalyzeAsync(RecordingId, TestContext.Current.CancellationToken));

        Assert.Equal("asr_not_configured", ex.Code);
    }

    [Fact]
    public async Task KhongCoTaiKhoanMay_ThiBaoMigrationChuaChay()
    {
        var harness = new Harness();
        harness.Repo.AiReviewerId = null;

        var ex = await Assert.ThrowsAsync<UnprocessableException>(
            () => harness.Service.AnalyzeAsync(RecordingId, TestContext.Current.CancellationToken));

        Assert.Equal("ai_reviewer_missing", ex.Code);
        Assert.Equal(0, harness.Asr.Calls);
    }

    [Fact]
    public async Task BanGhiDaChot_ThiKhongTieuMotLuotGoiAsrNao()
    {
        var harness = new Harness();
        harness.Repo.Candidate = Candidate(RecordingStatus.Approved);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => harness.Service.AnalyzeAsync(RecordingId, TestContext.Current.CancellationToken));

        Assert.Equal("recording_not_reviewable", ex.Code);
        Assert.Equal(0, harness.Asr.Calls);
    }

    [Fact]
    public async Task MayDaDuyetRoi_ThiKhongDuyetLan2()
    {
        var harness = new Harness();
        harness.Repo.AlreadyVoted = true;

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => harness.Service.AnalyzeAsync(RecordingId, TestContext.Current.CancellationToken));

        Assert.Equal("already_reviewed", ex.Code);
        Assert.Equal(0, harness.Asr.Calls);
    }

    // ---------------------------------------------------------------- luật phá thế

    [Theory]
    [InlineData(0, 0)] // chưa ai duyệt
    [InlineData(1, 1)] // mới một người, bảo đạt
    [InlineData(1, 0)] // mới một người, bảo không đạt
    [InlineData(2, 2)] // hai người CÙNG đạt — trigger đã chốt, máy không có việc
    [InlineData(2, 0)] // hai người CÙNG không đạt
    [InlineData(3, 2)] // đã đủ ba lượt người
    public async Task ChuaToiLuotMay_ThiChanVaKhongGoiAsr(int total, int approved)
    {
        var harness = new Harness();
        harness.Repo.Tally = new ReviewTally(total, approved);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => harness.Service.AnalyzeAsync(RecordingId, TestContext.Current.CancellationToken));

        Assert.Equal("ai_review_not_tiebreak", ex.Code);
        Assert.Empty(harness.Reviews.Submitted);

        // Quan trọng nhất: không tiêu tiền ASR cho bản chưa tới lượt.
        Assert.Equal(0, harness.Asr.Calls);
    }

    [Fact]
    public async Task HaiNguoiLechNhau_ThiMayVaoVaBoPhieuLuotThuBa()
    {
        var harness = new Harness();
        harness.Asr.Text = "Hôm nay trời mưa rất to.";

        var result = await harness.Service.AnalyzeAsync(
            RecordingId, TestContext.Current.CancellationToken);

        Assert.Equal(AiReviewVerdicts.Rejected, result.Verdict);
        Assert.Equal(1, harness.Asr.Calls);

        var submitted = Assert.Single(harness.Reviews.Submitted);
        Assert.Equal(ReviewDecision.Rejected, submitted.Decision);
        Assert.Equal([AiReviewEvaluator.ContentMismatch], submitted.RejectionReasonCodes);

        // Máy là phiếu thứ ba: đã có 2 lượt nên vòng của nó phải là 3.
        Assert.Equal(3, submitted.ExpectedRound);

        var evidence = Assert.Single(harness.Repo.Added);
        Assert.Equal(AiReviewVerdicts.Rejected, evidence.Verdict);
        Assert.True(evidence.ReviewId > 0, "bằng chứng phải trỏ tới lượt duyệt vừa ghi");
        Assert.True(harness.Transaction.Committed);
    }

    [Fact]
    public async Task DocDung_ThiMayBoPhieuDat_KhongCanBatCoGiThem()
    {
        // Không còn cờ allow_approve_vote: người phá thế phải phá được cả hai phía.
        var harness = new Harness();
        harness.Asr.Text = "Em nên scan tài liệu này.";

        var result = await harness.Service.AnalyzeAsync(
            RecordingId, TestContext.Current.CancellationToken);

        Assert.Equal(AiReviewVerdicts.Approved, result.Verdict);

        var submitted = Assert.Single(harness.Reviews.Submitted);
        Assert.Equal(ReviewDecision.Approved, submitted.Decision);
        Assert.Empty(submitted.RejectionReasonCodes);
    }

    [Fact]
    public async Task VungTungLaKhongChac_GioVanPhaiQuyet()
    {
        // WER khoảng 0.25: trước đây máy bỏ quyền, giờ phải chọn — và chọn đạt vì dưới 0.40.
        var harness = new Harness();
        harness.Asr.Text = "Em nên scan tài liệu.";

        var result = await harness.Service.AnalyzeAsync(
            RecordingId, TestContext.Current.CancellationToken);

        Assert.Equal(AiReviewVerdicts.Approved, result.Verdict);
        Assert.Single(harness.Reviews.Submitted);
    }

    [Fact]
    public async Task AsrMatDau_ThiKhongLoaiOanNguoiDoc()
    {
        var harness = new Harness();
        harness.Asr.Text = "Em nen scan tai lieu nay.";

        var result = await harness.Service.AnalyzeAsync(
            RecordingId, TestContext.Current.CancellationToken);

        Assert.Equal(AiReviewVerdicts.Approved, result.Verdict);
        Assert.Equal(0m, result.EffectiveWer);
        Assert.True(result.Wer > 0m, "WER giữ dấu phải khác 0 để bài test còn ý nghĩa");
    }

    [Fact]
    public async Task BinhLuan_KhongVuotTranCot1000KyTu()
    {
        var harness = new Harness();
        harness.Asr.Text = string.Join(' ', Enumerable.Repeat("lung tung", 500));

        await harness.Service.AnalyzeAsync(RecordingId, TestContext.Current.CancellationToken);

        var submitted = Assert.Single(harness.Reviews.Submitted);
        Assert.NotNull(submitted.Comment);
        Assert.True(submitted.Comment!.Length <= 1000,
            $"Bình luận dài {submitted.Comment.Length} ký tự, vượt trần 1000");
    }

    // ---------------------------------------------------------------- chạy lô

    [Fact]
    public async Task ChayLo_MotBanLoiKhongLamDoCaLo()
    {
        var harness = new Harness();
        harness.Repo.TiedIds = ["r_cs_211000001", RecordingId];
        harness.Asr.Text = "Hôm nay trời mưa rất to.";
        harness.Repo.MissingIds.Add("r_cs_211000001");

        var batch = await harness.Service.RunBatchAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(1, batch.Analyzed);
        Assert.Equal(1, batch.Rejected);

        var skipped = Assert.Single(batch.Skipped);
        Assert.Equal("r_cs_211000001", skipped.RecordingId);
        Assert.Equal("recording_not_found", skipped.Code);
    }

    [Fact]
    public async Task ChayLo_TonTrongTranLimit()
    {
        var harness = new Harness();
        harness.Repo.TiedIds = ["a", "b", "c", "d"];

        await harness.Service.RunBatchAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(2, harness.Asr.Calls);
    }

    private static AiReviewCandidate Candidate(RecordingStatus status) =>
        new(RecordingId, SentenceVariant.CodeSwitching, status, CloudLink, Expected, SpeakerId: 7);

    // ---------------------------------------------------------------- giàn thử

    private sealed class Harness
    {
        public bool Enabled { get; init; } = true;

        public StubAsr Asr { get; } = new();
        public StubRepo Repo { get; } = new();
        public StubReviews Reviews { get; } = new();
        public StubTransaction Transaction { get; } = new();

        public AiReviewService Service => new(
            Repo.With(Transaction),
            Reviews,
            new StubStorage(),
            Asr,
            new StubConfig(Enabled),
            Options.Create(new AudioOptions()),
            TimeProvider.System,
            NullLogger<AiReviewService>.Instance);
    }

    private sealed class StubAsr : IAsrClient
    {
        public bool Configured { get; set; } = true;
        public string Text { get; set; } = "Em nên scan tài liệu này.";
        public int Calls { get; private set; }

        public bool IsConfigured => Configured;

        public Task<AsrResult> TranscribeAsync(byte[] audio, string fileName, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new AsrResult(Text, "whisper-test"));
        }
    }

    private sealed class StubRepo : IAiReviewRepository
    {
        private StubTransaction _transaction = new();

        public long? AiReviewerId { get; set; } = 99;

        public AiReviewCandidate? Candidate { get; set; } =
            new(RecordingId, SentenceVariant.CodeSwitching, RecordingStatus.PendingReview,
                CloudLink, Expected, SpeakerId: 7);

        /// <summary>Mặc định là đúng thế cần phá: 2 lượt, 1 đạt / 1 không đạt.</summary>
        public ReviewTally Tally { get; set; } = new(Total: 2, Approved: 1);

        public bool AlreadyVoted { get; set; }
        public List<string> TiedIds { get; set; } = [];
        public List<AiReviewPendingItem> PendingRows { get; set; } = [];
        public HashSet<string> MissingIds { get; } = [];
        public List<AiReview> Added { get; } = [];

        public StubRepo With(StubTransaction transaction)
        {
            _transaction = transaction;
            return this;
        }

        public Task<long?> GetAiReviewerIdAsync(CancellationToken ct = default) =>
            Task.FromResult(AiReviewerId);

        public Task<AiReviewCandidate?> GetCandidateAsync(string recordingId, CancellationToken ct = default) =>
            Task.FromResult(MissingIds.Contains(recordingId) || Candidate is null
                ? null
                : Candidate with { RecordingId = recordingId });

        public Task<List<string>> GetTiedRecordingIdsAsync(
            int limit, long aiReviewerId, CancellationToken ct = default) =>
            Task.FromResult(TiedIds.Take(limit).ToList());

        public Task<List<AiReviewPendingItem>> GetPendingAsync(
            int limit, long aiReviewerId, CancellationToken ct = default) =>
            Task.FromResult(PendingRows.Take(limit).ToList());

        public Task<bool> HasVotedAsync(string recordingId, long aiReviewerId, CancellationToken ct = default) =>
            Task.FromResult(AlreadyVoted);

        public Task<ReviewTally> GetReviewTallyAsync(string recordingId, CancellationToken ct = default) =>
            Task.FromResult(Tally);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
            Task.FromResult<IDbContextTransaction>(_transaction);

        public Task<AiReview?> GetLatestAsync(string recordingId, CancellationToken ct = default) =>
            Task.FromResult<AiReview?>(null);

        public void Add(AiReview row)
        {
            // Database cấp khoá chính; giả lập để test còn so được ReviewId.
            row.AiReviewId = Added.Count + 1;
            Added.Add(row);
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubReviews : IReviewService
    {
        public List<SubmitReviewRequest> Submitted { get; } = [];

        public Task<SubmitReviewResult> SubmitAsync(
            string recordingId, long reviewerId, SubmitReviewRequest request, CancellationToken ct = default)
        {
            Submitted.Add(request);

            // Phiếu thứ ba làm một bên quá bán, nên bản ghi chốt luôn.
            var final = request.Decision == ReviewDecision.Approved
                ? RecordingStatus.Approved
                : RecordingStatus.Rejected;

            return Task.FromResult(new SubmitReviewResult(
                ReviewId: 500 + Submitted.Count,
                Round: 3,
                RoundsRequired: 3,
                ReviewsSoFar: 3,
                RecordingStatus: final,
                IsFinal: true));
        }

        public Task<NextReviewDto?> GetNextAsync(
            long reviewerId, long? taskId, long? speakerId, bool random, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PagedResult<TaskReviewItemDto>> GetTaskRecordingsAsync(
            long taskId, long reviewerId, TaskReviewQuery query, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<NextReviewDto> GetForReviewAsync(
            string recordingId, long reviewerId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RejectAudioResult> RejectAudioAsync(
            string recordingId, long reviewerId, RejectAudioRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<DiscardPairResult> DiscardPairAsync(
            string recordingId, long reviewerId, DiscardPairRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ReviewDto>> GetHistoryAsync(
            string recordingId, long viewerId, ViewerRole role, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ReviewerProgressDto> GetProgressAsync(long reviewerId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubStorage : IObjectStorage
    {
        public Task EnsureBucketAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task UploadAsync(string key, string filePath, string contentType, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

        public string GetObjectUrl(string key) => CloudLink;

        public string? GetObjectKey(string url) => "r_cs_211000000.wav";

        public Task<(string Url, DateTimeOffset ExpiresAt)> GetDownloadUrlAsync(
            string key, CancellationToken ct = default) =>
            Task.FromResult((CloudLink, DateTimeOffset.UtcNow.AddMinutes(15)));

        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("RIFF....WAVEfmt ")));
    }

    private sealed class StubConfig(bool enabled) : ISystemConfigService
    {
        public Task<IReadOnlyList<SystemConfigDto>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SystemConfigDto>>(
            [
                Row(ConfigKeys.AiReviewEnabled, enabled ? "true" : "false"),
                Row(ConfigKeys.AiReviewWerDecisionThreshold, "0.40")
            ]);

        private static SystemConfigDto Row(string key, string value) =>
            new(key, value, "string", null, DateTimeOffset.UtcNow);

        public Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default) =>
            Task.FromResult(fallback);

        public Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default) =>
            Task.FromResult(fallback);

        public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default) =>
            Task.FromResult(fallback);

        public Task<SystemConfigDto> GetAsync(string key, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(string key, string value, long updatedById, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubTransaction : IDbContextTransaction
    {
        public bool Committed { get; private set; }

        public Guid TransactionId { get; } = Guid.NewGuid();

        public void Commit() => Committed = true;

        public Task CommitAsync(CancellationToken ct = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public void Rollback() { }

        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
