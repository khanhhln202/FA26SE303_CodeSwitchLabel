using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Common;
using CodeSwitchLabel.Services.Dtos;

namespace CodeSwitchLabel.Services.Implementations;

public class ScriptAssignmentService(
    IScriptRepository repository,
    IScriptQueueService queue,
    ITaskRepository taskRepository,
    ISystemConfigService config) : IScriptAssignmentService
{
    /// <summary>
    /// Cặp câu tiếp theo người này nên thu.
    ///
    /// Mọi đường — trong task hay ngoài task — đều đi qua hàng đợi để giữ chỗ ngay
    /// lúc câu được load lên màn hình. Claim của chính người này được trả lại cho họ
    /// ở lần lấy kế tiếp, nhờ vậy thu xong bản cs vẫn nhận đúng câu để thu bản vi.
    /// </summary>
    public async Task<NextScriptDto?> GetNextAsync(
        long speakerId, long? taskId, IReadOnlyCollection<string>? exclude = null,
        CancellationToken ct = default)
    {
        if (taskId.HasValue)
        {
            // Không kiểm thì Speaker nào cũng xem được danh sách câu trong task của người khác.
            // Hàng đợi ghi âm phát theo chiến dịch, nên chỉ cần nhận một task thu âm của đợt.
            if (!await taskRepository.IsAssignedToRecordingScopeAsync(taskId.Value, speakerId, ct))
            {
                throw new ForbiddenException(
                    "task_not_recordable", $"Task #{taskId} không phải task thu âm đang giao cho bạn.");
            }
        }

        var excludedSet = exclude is { Count: > 0 }
            ? exclude.ToHashSet(StringComparer.Ordinal)
            : [];

        var script = await ClaimNextRecordableAsync(speakerId, taskId, excludedSet, ct);
        if (script is null) return null;

        var qc = RecordingQcSettings.From(await config.GetAllAsync(ct));
        var guidance = new RecordingGuidanceDto(
            qc.MinDurationSec, qc.MaxDurationSec, qc.MaxLeadingSilenceSec, qc.MaxTrailingSilenceSec);

        return new NextScriptDto(
            script.ScriptId,
            script.CsContent,
            CodeSwitchText.Strip(script.CsContent),
            script.ViContent,
            CodeSwitchText.Strip(script.ViContent),
            script.Domain,
            script.WordCount,
            script.EnWordCount,
            RemainingVariants(script, await repository.GetRecordingsOfSpeakerAsync(script.ScriptId, speakerId, ct)),
            guidance,
            ScriptPairRules.ToAlignment(script.Words),
            script.SemanticWarning);
    }

    public async Task<NextScriptDto?> SkipAsync(
        long speakerId, string scriptId, long? taskId, CancellationToken ct = default)
    {
        if (taskId.HasValue &&
            !await taskRepository.IsAssignedToRecordingScopeAsync(taskId.Value, speakerId, ct))
        {
            throw new ForbiddenException(
                "task_not_recordable", $"Task #{taskId} không phải task thu âm đang giao cho bạn.");
        }

        // Chỉ nhả claim của chính mình; người khác đang giữ thì không đụng.
        await queue.ReleaseClaimOfAsync(scriptId, QueueStage.Recording, speakerId, ct);

        return await GetNextAsync(speakerId, taskId, [scriptId], ct);
    }

    /// <summary>
    /// Hàng đợi có thể đứng đầu bằng câu người này đã thu đủ, hoặc câu do người khác
    /// đang giữ. Lấy tối đa vài câu liên tiếp rồi trả câu đầu tiên dùng được.
    /// Số lần thử có chặn trên, không phải vô hạn.
    /// </summary>
    private const int MaxClaimAttempts = 10;

    private async Task<Script?> ClaimNextRecordableAsync(
        long speakerId, long? taskId, HashSet<string> excluded, CancellationToken ct)
    {
        var attempts = MaxClaimAttempts + excluded.Count;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var claim = await queue.ClaimNextForRecordingAsync(speakerId, taskId, ct);
            if (claim is null) return null;

            var script = await repository.GetAsync(claim.ScriptId, ct);
            if (script is null) continue;

            // Câu Speaker vừa bỏ qua: đẩy về cuối hàng, KHÔNG đánh dấu xong; hết phiên
            // tải lại trang thì câu quay lại bình thường.
            if (excluded.Contains(script.ScriptId))
            {
                await queue.ReleaseClaimOfAsync(script.ScriptId, QueueStage.Recording, speakerId, ct);
                continue;
            }

            // Bản của Speaker khác không chặn (mỗi người thu bản riêng); chỉ bỏ qua
            // câu người này đã thu đủ.
            var mine = await repository.GetRecordingsOfSpeakerAsync(script.ScriptId, speakerId, ct);

            if (RemainingVariants(script, mine).Count == 0)
            {
                await queue.CompleteAsync(script.ScriptId, QueueStage.Recording, ct);
                continue;
            }

            return script;
        }

        return null;
    }

    /// <summary>
    /// Biến thể người đọc còn nợ. Bản đã nộp đang chờ duyệt hoặc đã đạt thì coi như
    /// xong; bản bị từ chối hay trượt kiểm tra tự động thì vẫn phải thu lại.
    /// </summary>
    private static IReadOnlyList<SentenceVariant> RemainingVariants(
        Script script, IReadOnlyList<Recording> mine)
    {
        var done = mine
            .Where(r => r.Status is RecordingStatus.PendingReview or RecordingStatus.Approved)
            .Select(r => r.SentenceVariant)
            .ToHashSet();

        return [.. new[] { SentenceVariant.CodeSwitching, SentenceVariant.PureVietnamese }
            .Where(v => !done.Contains(v))];
    }
}
