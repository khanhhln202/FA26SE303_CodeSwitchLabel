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
    /// Làm trong task: đọc thẳng các dòng task_script giao cho chính người này (hoặc
    /// chưa gán ai), nên hai Speaker được nhận cùng một câu. Thu ngoài task: vẫn giữ
    /// chỗ theo hàng đợi chung để hai người không cùng nhận một câu.
    /// </summary>
    public async Task<NextScriptDto?> GetNextAsync(
        long speakerId, long? taskId, CancellationToken ct = default)
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

        var script = taskId.HasValue
            ? await FindAssignedAsync(speakerId, taskId.Value, ct)
            : await ClaimNextRecordableAsync(speakerId, null, ct);
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
            guidance);
    }

    /// <summary>
    /// Câu được giao trong task mà người này còn nợ ít nhất một biến thể. Không giữ
    /// chỗ: nhiều Speaker cùng nhận một câu là hợp lệ, mỗi người thu bản của mình.
    /// </summary>
    private async Task<Script?> FindAssignedAsync(long speakerId, long taskId, CancellationToken ct)
    {
        var ids = await taskRepository.GetAssignedScriptIdsAsync(
            speakerId, taskId, MaxClaimAttempts, ct);

        foreach (var id in ids)
        {
            var script = await repository.GetAsync(id, ct);
            if (script is null) continue;

            var mine = await repository.GetRecordingsOfSpeakerAsync(id, speakerId, ct);
            if (RemainingVariants(script, mine).Count > 0) return script;
        }

        return null;
    }

    /// <summary>
    /// Hàng đợi có thể đứng đầu bằng câu người này đã thu đủ, hoặc câu do người khác
    /// đang giữ. Lấy tối đa vài câu liên tiếp rồi trả câu đầu tiên dùng được.
    /// Số lần thử có chặn trên, không phải vô hạn.
    /// </summary>
    private const int MaxClaimAttempts = 10;

    private async Task<Script?> ClaimNextRecordableAsync(long speakerId, long? taskId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxClaimAttempts; attempt++)
        {
            var claim = await queue.ClaimNextForRecordingAsync(speakerId, taskId, ct);
            if (claim is null) return null;

            var script = await repository.GetAsync(claim.ScriptId, ct);
            if (script is null) continue;

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
