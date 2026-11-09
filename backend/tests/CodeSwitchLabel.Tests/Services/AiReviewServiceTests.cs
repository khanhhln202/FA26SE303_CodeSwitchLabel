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
/// Máy duyệt: khi nào bỏ phiếu thật, khi nào bỏ quyền, và những cửa chặn trước khi
/// tiêu một lượt gọi ASR có tính phí.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AiReviewServiceTests
{
    private const string RecordingId = "r_cs_211000000";
    private const string Expected = "[vi]Em nên [en]scan [vi]tài liệu này.";
    private const string CloudLink = "http://minio:9000/recordings/r_cs_211000000.wav";

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
        // Cửa chặn này đáng giá tiền thật: mỗi lượt gọi ASR là một lần tính phí.
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

    [Fact]
    public async Task DocLechHoanToan_ThiBoPhieuTuChoiVoiLyDoSaiNoiDung()
    {
        var harness = new Harness();
        harness.Asr.Text = "Hôm nay trời mưa rất to.";

        var result = await harness.Service.AnalyzeAsync(
            RecordingId, TestContext.Current.CancellationToken);

        Assert.Equal(AiReviewVerdicts.Rejected, result.Verdict);

        var submitted = Assert.Single(harness.Reviews.Submitted);
        Assert.Equal(ReviewDecision.Rejected, submitted.Decision);
        Assert.Equal([AiReviewEvaluator.ContentMismatch], submitted.RejectionReasonCodes);

        // Bằng chứng phải trỏ tới đúng lượt duyệt vừa ghi — ràng buộc
        // ck_ai_review_vote_has_review dưới database bắt chặt điều này.
        var evidence = Assert.Single(harness.Repo.Added);
        Assert.Equal(AiReviewVerdicts.Rejected, evidence.Verdict);
        Assert.NotNull(evidence.ReviewId);
        Assert.True(harness.Transaction.Committed);
    }

    [Fact]
    public async Task DocDung_NhungChuaBatChoBoPhieuDat_ThiBoQuyenVaKhongGhiLuotDuyet()
    {
        var harness = new Harness { AllowApproveVote = false };
        harness.Asr.Text = "Em nên scan tài liệu này.";

        var result = await harness.Service.AnalyzeAsync(
            RecordingId, TestContext.Current.CancellationToken);

        Assert.Equal(AiReviewVerdicts.Abstained, result.Verdict);
        Assert.Empty(harness.Reviews.Submitted);

        var evidence = Assert.Single(harness.Repo.Added);
        Assert.Null(evidence.ReviewId);
        Assert.Null(result.ReviewId);
    }

    [Fact]
    public async Task DocDung_VaDaBatChoBoPhieuDat_ThiBoPhieuDat()
    {
        var harness = new Harness { AllowApproveVote = true };
        harness.Asr.Text = "Em nên scan tài liệu này.";

        var result = await harness.Service.AnalyzeAsync(
            RecordingId, TestContext.Current.CancellationToken);

        Assert.Equal(AiReviewVerdicts.Approved, result.Verdict);

        var submitted = Assert.Single(harness.Reviews.Submitted);
        Assert.Equal(ReviewDecision.Approved, submitted.Decision);
        Assert.Empty(submitted.RejectionReasonCodes);
    }

    [Fact]
    public async Task AsrMatDau_ThiKhongLoaiOanNguoiDoc()
    {
        var harness = new Harness();
        harness.Asr.Text = "Em nen scan tai lieu nay.";

        var result = await harness.Service.AnalyzeAsync(
            RecordingId, TestContext.Current.CancellationToken);

        Assert.Empty(harness.Reviews.Submitted);
        Assert.Equal(0m, result.EffectiveWer);
        Assert.True(result.Wer > 0m, "WER giữ dấu phải khác 0 để bài test còn ý nghĩa");
    }

    [Fact]
    public async Task BinhLuan_KhongVuotTranCot1000KyTu()
    {
        var harness = new Harness();
        // ASR trả về một chuỗi rác rất dài: review.comment là VARCHAR(1000).
        harness.Asr.Text = string.Join(' ', Enumerable.Repeat("lung tung", 500));

        await harness.Service.AnalyzeAsync(RecordingId, TestContext.Current.CancellationToken);

        var submitted = Assert.Single(harness.Reviews.Submitted);
        Assert.NotNull(submitted.Comment);
        Assert.True(submitted.Comment!.Length <= 1000,
            $"Bình luận dài {submitted.Comment.Length} ký tự, vượt trần 1000");
    }

    [Fact]
    public async Task ChayLo_MotBanLoiKhongLamDoCaLo()
    {
        var harness = new Harness();
        harness.Repo.PendingIds = ["r_cs_211000001", RecordingId];
        harness.Asr.Text = "Hôm nay trời mưa rất to.";

        // Bản đầu tiên không tra được: coi như vừa bị xoá giữa lúc chạy lô.
        harness.Repo.MissingIds.Add("r_cs_211000001");

        var batch = await harness.Service.RunBatchAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(1, batch.Analyzed);
        Assert.Equal(1, batch.Rejected);

        var skipped = Assert.Single(batch.Skipped);
        Assert.Equal("r_cs_211000001", skipped.RecordingId);
        Assert.Equal("recording_not_found", skipped.Code);
    }

    private static AiReviewCandidate Candidate(RecordingStatus status) =>
        new(RecordingId, SentenceVariant.CodeSwitching, status, CloudLink, Expected, SpeakerId: 7);

    // ---------------------------------------------------------------- giàn thử

    private sealed class Harness
    {
        public bool Enabled { get; init; } = true;
        public bool AllowApproveVote { get; init; }

        public StubAsr Asr { get; } = new();
        public StubRepo Repo { get; } = new();
        public StubReviews Reviews { get; } = new();
        public StubTransaction Transaction { get; } = new();

        public AiReviewService Service => new(
            Repo.With(Transaction),
            Reviews,
            new StubStorage(),
            Asr,
            new StubConfig(Enabled, AllowApproveVote),
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

        public bool AlreadyVoted { get; set; }
        public List<string> PendingIds { get; set; } = [];
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

        public Task<List<string>> GetPendingRecordingIdsAsync(
            int limit, long aiReviewerId, CancellationToken ct = default) =>
            Task.FromResult(PendingIds.Take(limit).ToList());

        public Task<bool> HasVotedAsync(string recordingId, long aiReviewerId, CancellationToken ct = default) =>
            Task.FromResult(AlreadyVoted);

        public Task<int> GetReviewCountAsync(string recordingId, CancellationToken ct = default) =>
            Task.FromResult(0);

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

            return Task.FromResult(new SubmitReviewResult(
                ReviewId: 500 + Submitted.Count,
                Round: 1,
                RoundsRequired: 3,
                ReviewsSoFar: 1,
                RecordingStatus: RecordingStatus.PendingReview,
                IsFinal: false));
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

    private sealed class StubConfig(bool enabled, bool allowApproveVote) : ISystemConfigService
    {
        public Task<IReadOnlyList<SystemConfigDto>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SystemConfigDto>>(
            [
                Row(ConfigKeys.AiReviewEnabled, enabled ? "true" : "false"),
                Row(ConfigKeys.AiReviewWerRejectMin, "0.40"),
                Row(ConfigKeys.AiReviewWerApproveMax, "0.10"),
                Row(ConfigKeys.AiReviewAllowApproveVote, allowApproveVote ? "true" : "false")
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
