using System.Globalization;
using System.Text.Json;
using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;
using CodeSwitchLabel.Services.Reviews;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSwitchLabel.Services.Implementations;

/// <summary>
/// Máy duyệt bản ghi, đóng vai PHIẾU THỨ BA PHÁ THẾ.
///
/// Luật nghiệp vụ (chốt với giảng viên hướng dẫn): hai Reviewer người duyệt trước. Cùng kết
/// quả thì trigger trg_review_majority chốt ngay ở lượt 2 và máy không tham gia. Chỉ khi hai
/// người LỆCH nhau (1 đạt / 1 không đạt) thì máy mới vào. Vì là người phá thế, máy luôn phải
/// chọn một bên — không có "bỏ quyền", vì bỏ quyền để lại bản ghi 1–1 không ai quyết.
///
/// Bỏ phiếu bằng cách gọi đúng <see cref="IReviewService.SubmitAsync"/> mà người dùng đi,
/// KHÔNG tự INSERT vào bảng review. Hàm đó đã khoá hàng recording trong transaction (nên
/// không đụng số review_round của ràng buộc UNIQUE), chặn tự duyệt, chặn duyệt hai lần, và
/// bắt buộc có lý do khi từ chối. Tự INSERT là viết lại từng cái đó rồi quên một cái.
///
/// Máy chỉ nêu được lý do content_mismatch. Ba lý do active còn lại — background_noise,
/// mispronunciation, other — nó không đo được nên không bao giờ tự chọn.
/// </summary>
public class AiReviewService(
    IAiReviewRepository repo,
    IReviewService reviews,
    IObjectStorage storage,
    IAsrClient asr,
    ISystemConfigService config,
    IOptions<AudioOptions> audioOptions,
    TimeProvider clock,
    ILogger<AiReviewService> logger) : IAiReviewService
{
    /// <summary>review.comment là VARCHAR(1000) nên phải tự cắt trước khi gửi xuống.</summary>
    private const int CommentLimit = 1000;

    private readonly AudioOptions _audio = audioOptions.Value;

    public async Task<AiReviewResultDto> AnalyzeAsync(string recordingId, CancellationToken ct = default)
    {
        var settings = await LoadSettingsAsync(ct);
        var aiReviewerId = await RequireAiReviewerAsync(ct);

        return await AnalyzeOneAsync(recordingId, aiReviewerId, settings, ct);
    }

    public async Task<AiReviewBatchResultDto> RunBatchAsync(int limit, CancellationToken ct = default)
    {
        var settings = await LoadSettingsAsync(ct);
        var aiReviewerId = await RequireAiReviewerAsync(ct);

        var ids = await repo.GetTiedRecordingIdsAsync(limit, aiReviewerId, ct);

        var items = new List<AiReviewResultDto>();
        var skipped = new List<AiReviewSkippedDto>();

        foreach (var id in ids)
        {
            try
            {
                items.Add(await AnalyzeOneAsync(id, aiReviewerId, settings, ct));
            }
            catch (AppException ex)
            {
                // Một bản lỗi không được làm đổ cả lô: ASR hỏng giữa đường, hoặc có người
                // vừa bỏ phiếu xong làm bản ghi không còn ở thế 1–1. Ghi lại rồi đi tiếp.
                logger.LogWarning(ex, "Bỏ qua {RecordingId} khi chạy lô máy duyệt: {Code}", id, ex.Code);
                skipped.Add(new AiReviewSkippedDto(id, ex.Code, ex.Message));
            }
        }

        return new AiReviewBatchResultDto(
            items.Count,
            items.Count(i => i.Verdict == AiReviewVerdicts.Approved),
            items.Count(i => i.Verdict == AiReviewVerdicts.Rejected),
            items,
            skipped);
    }

    public async Task<AiReviewResultDto?> GetLatestAsync(string recordingId, CancellationToken ct = default)
    {
        var row = await repo.GetLatestAsync(recordingId, ct);

        if (row is null) return null;

        return new AiReviewResultDto(
            row.RecordingId,
            row.Verdict,
            row.Wer,
            row.WerFolded,
            Math.Min(row.Wer, row.WerFolded),
            row.Transcript,
            row.ExpectedText,
            ReadExplanation(row.Details),
            row.Model,
            row.ReviewId,
            RecordingStatus: null,
            row.AnalyzedAt);
    }

    private async Task<AiReviewResultDto> AnalyzeOneAsync(
        string recordingId, long aiReviewerId, AiReviewSettings settings, CancellationToken ct)
    {
        var candidate = await repo.GetCandidateAsync(recordingId, ct)
            ?? throw new NotFoundException(
                "recording_not_found", $"Không tìm thấy bản ghi {recordingId}.");

        // Kiểm TRƯỚC khi gọi ASR: mỗi lượt gọi là một lần tốn tiền, không gọi cho bản
        // đã chốt rồi.
        if (candidate.Status != RecordingStatus.PendingReview)
        {
            throw new ConflictException(
                "recording_not_reviewable",
                $"Bản ghi {recordingId} đang ở trạng thái {candidate.Status}, không còn chờ duyệt.");
        }

        if (await repo.HasVotedAsync(recordingId, aiReviewerId, ct))
        {
            throw new ConflictException(
                "already_reviewed", $"Máy đã duyệt bản ghi {recordingId} rồi.");
        }

        // CỬA CHẶN CHÍNH của luật nghiệp vụ: hai Reviewer người duyệt trước, và máy chỉ
        // vào khi hai người đó LỆCH nhau. Kiểm trước khi gọi ASR vì mỗi lượt gọi tốn tiền.
        var tally = await repo.GetReviewTallyAsync(recordingId, ct);

        if (!tally.IsTie)
        {
            throw new ConflictException(
                "ai_review_not_tiebreak",
                $"Bản ghi {recordingId} chưa tới lượt máy: đang có {tally.Total} lượt duyệt "
                + $"({tally.Approved} đạt / {tally.Total - tally.Approved} không đạt). "
                + "Máy chỉ vào khi đã có đúng hai lượt của người và hai lượt đó khác kết quả.");
        }

        var audio = await DownloadAsync(candidate.CloudLink, recordingId, ct);
        var transcription = await asr.TranscribeAsync(audio, $"{recordingId}.wav", ct);

        var comparison = TranscriptComparison.Compare(candidate.ExpectedRaw, transcription.Text);
        var decision = AiReviewEvaluator.Evaluate(comparison.Effective, settings);

        return await PersistAsync(
            candidate, aiReviewerId, settings, transcription, comparison, decision, ct);
    }

    /// <summary>
    /// Lượt bỏ phiếu và dòng bằng chứng vào cùng một transaction. Thiếu bước này thì có thể
    /// có phiếu mà không có bằng chứng, hoặc ngược lại — mà ràng buộc
    /// ck_ai_review_vote_has_review dưới database không cho dòng bằng chứng nào nói
    /// "đã từ chối" mà chẳng trỏ tới lượt duyệt nào.
    /// </summary>
    private async Task<AiReviewResultDto> PersistAsync(
        AiReviewCandidate candidate,
        long aiReviewerId,
        AiReviewSettings settings,
        AsrResult transcription,
        TranscriptComparisonResult comparison,
        AiReviewDecision decision,
        CancellationToken ct)
    {
        await using var transaction = await repo.BeginTransactionAsync(ct);

        // Đọc lại số lượt BÊN TRONG transaction: SubmitAsync khoá hàng recording và bắt
        // ExpectedRound khớp vòng hiện tại, nên con số phải lấy sau khi đã vào transaction.
        var tally = await repo.GetReviewTallyAsync(candidate.RecordingId, ct);

        var submitted = await reviews.SubmitAsync(
            candidate.RecordingId,
            aiReviewerId,
            new SubmitReviewRequest
            {
                Decision = decision.Verdict == AiReviewVerdict.Approved
                    ? ReviewDecision.Approved
                    : ReviewDecision.Rejected,
                ExpectedRound = ReviewRules.NextRound(tally.Total),
                RejectionReasonCodes = [.. decision.RejectionReasonCodes],
                Comment = BuildComment(decision, comparison, transcription)
            },
            ct);

        var row = new AiReview
        {
            RecordingId = candidate.RecordingId,
            ReviewId = submitted.ReviewId,
            Model = transcription.Model,
            Transcript = transcription.Text,
            ExpectedText = comparison.ExpectedNormalized,
            Wer = comparison.Wer,
            WerFolded = comparison.WerFolded,
            Verdict = ToColumn(decision.Verdict),
            AnalyzedAt = clock.GetUtcNow(),
            Details = BuildDetails(decision, settings, comparison)
        };

        repo.Add(row);
        await repo.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Máy duyệt {RecordingId}: {Verdict}, WER {Wer} (bỏ dấu {Folded}), model {Model}.",
            candidate.RecordingId, row.Verdict, comparison.Wer, comparison.WerFolded, row.Model);

        return new AiReviewResultDto(
            candidate.RecordingId,
            row.Verdict,
            comparison.Wer,
            comparison.WerFolded,
            comparison.Effective,
            transcription.Text,
            comparison.ExpectedNormalized,
            decision.Explanation,
            transcription.Model,
            submitted.ReviewId,
            submitted.RecordingStatus,
            row.AnalyzedAt);
    }

    private async Task<AiReviewSettings> LoadSettingsAsync(CancellationToken ct)
    {
        var settings = AiReviewSettings.From(await config.GetAllAsync(ct));

        if (!settings.Enabled)
        {
            throw new UnprocessableException(
                "ai_review_disabled",
                $"Tính năng máy duyệt đang tắt. Bật bằng cách đặt {ConfigKeys.AiReviewEnabled} = true.");
        }

        if (!asr.IsConfigured)
        {
            throw new UnprocessableException(
                "asr_not_configured",
                "Chưa cấu hình Asr:Endpoint và Asr:Model nên không chuyển được âm thanh thành chữ.");
        }

        return settings;
    }

    private async Task<long> RequireAiReviewerAsync(CancellationToken ct) =>
        await repo.GetAiReviewerIdAsync(ct)
        ?? throw new UnprocessableException(
            "ai_reviewer_missing",
            $"Không có tài khoản {AiReviewer.Email}. Migration V24 chưa chạy trên database này.");

    private async Task<byte[]> DownloadAsync(string cloudLink, string recordingId, CancellationToken ct)
    {
        var key = storage.GetObjectKey(cloudLink)
            ?? throw new UnprocessableException(
                "recording_audio_missing",
                $"Địa chỉ file của bản ghi {recordingId} không thuộc kho lưu trữ đang cấu hình.");

        await using var source = await storage.OpenReadAsync(key, ct);
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);

        var bytes = buffer.ToArray();
        var limit = _audio.MaxUploadMegabytes * 1024L * 1024L;

        // Trần theo đúng trần upload: file trong kho đáng ra không thể lớn hơn, nhưng giữ
        // toàn bộ vào bộ nhớ thì phải có chặn, không để một object lạ làm hết RAM máy chủ.
        if (bytes.LongLength > limit)
        {
            throw new UnprocessableException(
                "recording_audio_too_large",
                $"File của bản ghi {recordingId} lớn hơn mức cho phép {_audio.MaxUploadMegabytes} MB.");
        }

        return bytes;
    }

    /// <summary>
    /// Bình luận để NGƯỜI đọc hiểu vì sao máy quyết như vậy, nên kèm cả transcript và văn bản
    /// gốc. Cắt theo tỉ lệ chứ không cắt cụt một đầu: mất hẳn một trong hai thì không so được.
    /// </summary>
    private static string BuildComment(
        AiReviewDecision decision, TranscriptComparisonResult comparison, AsrResult transcription)
    {
        const string prefix = "[Máy duyệt] ";
        var head = prefix + decision.Explanation;

        var remaining = CommentLimit - head.Length - 40;

        if (remaining < 80) return Cut(head, CommentLimit);

        var half = remaining / 2;

        return Cut(
            head
            + $" Nghe được: «{Cut(transcription.Text, half)}»"
            + $" Văn bản: «{Cut(comparison.ExpectedNormalized, half)}»",
            CommentLimit);
    }

    private static string BuildDetails(
        AiReviewDecision decision, AiReviewSettings settings, TranscriptComparisonResult comparison) =>
        JsonSerializer.Serialize(new
        {
            explanation = decision.Explanation,
            effective_wer = comparison.Effective,
            wer_decision_threshold = settings.WerDecisionThreshold,
            reason_codes = decision.RejectionReasonCodes
        });

    private static string ReadExplanation(string? details)
    {
        if (string.IsNullOrWhiteSpace(details)) return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(details);

            return document.RootElement.TryGetProperty("explanation", out var value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            // Dòng cũ ghi bằng phiên bản khác thì vẫn trả phần còn lại, không ném 500.
            return string.Empty;
        }
    }

    private static string ToColumn(AiReviewVerdict verdict) =>
        verdict == AiReviewVerdict.Approved ? AiReviewVerdicts.Approved : AiReviewVerdicts.Rejected;

    private static string Cut(string value, int limit) =>
        string.IsNullOrEmpty(value) || value.Length <= limit
            ? value ?? string.Empty
            : value[..Math.Max(0, limit - 1)] + "…";
}
