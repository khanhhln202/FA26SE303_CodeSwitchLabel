using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>Luồng làm việc của Speaker.</summary>
[ApiController]
[Route("api/speaker")]
[Tags(ApiTags.Speaker)]
[Authorize(Roles = "Speaker")]
public class SpeakerController(
    IScriptAssignmentService assignmentService,
    IScriptService scriptService,
    ITaskService taskService,
    ISpeakerRoundsService roundsService) : ControllerBase
{
    /// <summary>Lấy cặp câu tiếp theo để thu âm.</summary>
    /// <remarks>
    /// Mỗi cặp câu cần **hai bản ghi**: một bản đọc câu chen tiếng Anh (cs) và một bản đọc câu
    /// thuần Việt (vi). `remainingVariants` cho biết bạn còn nợ bản nào.
    ///
    /// Một cặp câu chỉ do **một người đọc** thu, để hai bản là cùng một giọng. Vì vậy hệ thống
    /// không phát lại câu mà người khác đã thu.
    ///
    /// Truyền `taskId` để chỉ lấy câu trong phạm vi một task. Task đó phải là task thu âm
    /// **đang giao cho chính bạn**, nếu không trả **403**.
    ///
    /// **204 No Content** nghĩa là hết câu phù hợp.
    /// </remarks>
    [HttpGet("scripts/next")]
    [ProducesResponseType(typeof(NextScriptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NextScriptDto>> Next([FromQuery] long? taskId, CancellationToken ct)
    {
        var next = await assignmentService.GetNextAsync(User.GetUserId(), taskId, ct);
        return next is null ? NoContent() : Ok(next);
    }

    /// <summary>Đóng góp một cặp câu mới.</summary>
    /// <remarks>
    /// Phải gửi đủ cả câu chen tiếng Anh lẫn câu thuần Việt tương đương, kèm nhãn [vi]/[en].
    /// Câu đóng góp nằm ở trạng thái chờ duyệt nội dung.
    ///
    /// Thấy câu được giao không tự nhiên hay sai chính tả thì dùng
    /// `POST /api/scripts/{id}/review` để sửa hoặc từ chối, thay vì bỏ qua.
    /// </remarks>
    [HttpPost("scripts/contribute")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScriptDetailDto>> Contribute(
        [FromBody] CreateScriptRequest request, CancellationToken ct)
    {
        var created = await scriptService.ContributeAsync(request, User.GetUserId(), ct);

        return CreatedAtAction(nameof(ScriptsController.Get), "Scripts",
            new { id = created.ScriptId }, created);
    }

    /// <summary>Tiến độ thu âm của chính mình.</summary>
    /// <remarks>
    /// Tổng số bản đã nộp, số bản được duyệt đạt, và từng task thu âm đang giao.
    /// Chỉ tiêu task thu âm đếm theo **cặp câu**: một cặp chỉ tính là xong khi **cả hai** bản
    /// cs và vi đều được duyệt đạt.
    /// </remarks>
    [HttpGet("progress")]
    [ProducesResponseType(typeof(SpeakerProgressDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakerProgressDto>> Progress(CancellationToken ct)
        => Ok(await taskService.GetSpeakerProgressAsync(User.GetUserId(), ct));

    /// <summary>Đợt đang tham gia (chiến dịch của task thu âm đang giao). 204 = chưa tham gia đợt nào.</summary>
    [HttpGet("rounds/current")]
    [ProducesResponseType(typeof(SpeakerRoundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<SpeakerRoundDto>> CurrentRound(CancellationToken ct)
    {
        var round = await roundsService.GetCurrentAsync(User.GetUserId(), ct);
        return round is null ? NoContent() : Ok(round);
    }

    /// <summary>Các đợt đang mở đăng ký.</summary>
    [HttpGet("rounds/upcoming")]
    [ProducesResponseType(typeof(IEnumerable<UpcomingRoundDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UpcomingRoundDto>>> UpcomingRounds(CancellationToken ct)
        => Ok(await roundsService.GetUpcomingAsync(User.GetUserId(), ct));

    /// <summary>Bảng xếp hạng của một đợt, xếp theo số bản được duyệt.</summary>
    [HttpGet("rounds/{campaignId:long}/leaderboard")]
    [ProducesResponseType(typeof(IEnumerable<LeaderboardEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<LeaderboardEntryDto>>> Leaderboard(
        long campaignId, [FromQuery] int top = 5, CancellationToken ct = default)
        => Ok(await roundsService.GetLeaderboardAsync(campaignId, User.GetUserId(), top, ct));

    /// <summary>Đăng ký tham gia một đợt.</summary>
    [HttpPost("rounds/{campaignId:long}/register")]
    [ProducesResponseType(typeof(SpeakerRoundDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SpeakerRoundDto>> Register(long campaignId, CancellationToken ct)
        => Ok(await roundsService.RegisterAsync(campaignId, User.GetUserId(), ct));

    /// <summary>Huỷ đăng ký một đợt.</summary>
    [HttpDelete("rounds/{campaignId:long}/registration")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unregister(long campaignId, CancellationToken ct)
    {
        await roundsService.UnregisterAsync(campaignId, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Bản ghi bị từ chối của chính mình, kèm lý do và link nghe lại để thu lại.</summary>
    [HttpGet("recordings/rejected")]
    [ProducesResponseType(typeof(IEnumerable<RejectedRecordingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RejectedRecordingDto>>> Rejected(
        [FromQuery] int limit = 5, CancellationToken ct = default)
        => Ok(await roundsService.GetRejectedAsync(User.GetUserId(), limit, ct));

    /// <summary>Lịch sử ghi âm của chính mình, kèm kết quả duyệt đã chốt.</summary>
    [HttpGet("recordings/history")]
    [ProducesResponseType(typeof(PagedResult<SpeakerRecordingHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SpeakerRecordingHistoryDto>>> RecordingHistory(
        [FromQuery] SpeakerHistoryQuery query, CancellationToken ct)
        => Ok(await roundsService.GetRecordingHistoryAsync(User.GetUserId(), query, ct));

    /// <summary>Lịch sử đóng góp câu của chính mình.</summary>
    [HttpGet("contributions/history")]
    [ProducesResponseType(typeof(PagedResult<SpeakerContributionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SpeakerContributionHistoryDto>>> ContributionHistory(
        [FromQuery] SpeakerContributionHistoryQuery query, CancellationToken ct)
        => Ok(await roundsService.GetContributionHistoryAsync(User.GetUserId(), query, ct));

    /// <summary>Tổng hợp số bản đã nộp / đạt / bị từ chối / đang chờ của chính mình.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(SpeakerStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeakerStatsDto>> Stats(CancellationToken ct)
        => Ok(await roundsService.GetStatsAsync(User.GetUserId(), ct));
}
