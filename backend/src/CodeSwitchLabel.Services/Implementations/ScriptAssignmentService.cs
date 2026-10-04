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
    ITaskRepository taskRepository,
    ISystemConfigService config) : IScriptAssignmentService
{
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

        var script = await repository.GetNextForSpeakerAsync(speakerId, taskId, ct);
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
            RemainingVariants(script),
            guidance);
    }

    /// <summary>
    /// Biến thể người đọc còn nợ. Bản đã nộp đang chờ duyệt hoặc đã đạt thì coi như xong;
    /// bản bị từ chối hay trượt kiểm tra tự động thì vẫn phải thu lại.
    /// </summary>
    private static IReadOnlyList<SentenceVariant> RemainingVariants(Script script)
    {
        var done = script.Recordings
            .Where(r => r.Status is RecordingStatus.PendingReview or RecordingStatus.Approved)
            .Select(r => r.SentenceVariant)
            .ToHashSet();

        return [.. new[] { SentenceVariant.CodeSwitching, SentenceVariant.PureVietnamese }
            .Where(v => !done.Contains(v))];
    }
}
