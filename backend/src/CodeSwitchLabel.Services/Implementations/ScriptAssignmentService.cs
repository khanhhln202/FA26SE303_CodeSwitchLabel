using CodeSwitchLabel.Repositories.Entities;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Repositories;
using CodeSwitchLabel.Services.Abstractions;
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
    /// Cặp câu tiếp theo người này nên thu, theo thứ tự FIFO của hàng đợi chung.
    ///
    /// trước đây hàm này CHỈ ĐỌC — trả câu cho người này nhưng không giữ
    /// lại, nên hai người có thể cùng nhìn thấy một câu rồi cùng thu. Chống trùng chỉ
    /// chạy ở lúc UPLOAD (uq_recording_active_variant + trg_recording_single_speaker),
    /// tức là người đã đọc xong mới biết mình bị trùng (yêu cầu 3.2).
    /// Giờ câu được giữ ngay khi phát ra, nên không ai cùng nhận được.
    /// </summary>
    public async Task<NextScriptDto?> GetNextAsync(
        long speakerId, long? taskId, CancellationToken ct = default)
    {
        if (taskId.HasValue)
        {
            // Không kiểm thì Speaker nào cũng xem được danh sách câu trong task của người khác.
            if (!await taskRepository.IsActiveTaskOfAsync(taskId.Value, speakerId, TaskType.Recording, ct))
            {
                throw new ForbiddenException(
                    "task_not_recordable", $"Task #{taskId} không phải task thu âm đang giao cho bạn.");
            }
        }

        var script = await ClaimNextRecordableAsync(speakerId, taskId, ct);
        if (script is null) return null;

        var guidance = new RecordingGuidanceDto(
            await config.GetDecimalAsync(ConfigKeys.RecordingMinDurationSec, 1m, ct),
            await config.GetDecimalAsync(ConfigKeys.RecordingMaxDurationSec, 30m, ct),
            await config.GetDecimalAsync(ConfigKeys.RecordingMaxLeadingSilenceSec, 1m, ct),
            await config.GetDecimalAsync(ConfigKeys.RecordingMaxTrailingSilenceSec, 1m, ct));

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

            // Câu do người khác đọc — trigger sẽ chặn lúc thu, nên đừng phát ra.
            if (await repository.IsRecordedByOtherAsync(script.ScriptId, speakerId, ct))
            {
                await queue.ReleaseAsync(script.ScriptId, QueueStage.Recording, ct);
                continue;
            }

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
