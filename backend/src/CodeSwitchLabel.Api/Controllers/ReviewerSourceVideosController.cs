using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// Hàng chờ Reviewer cho corpus hội thoại. Mỗi Reviewer giữ đúng một video một lúc;
/// nhận xong thì video bị khoá cho tới khi duyệt hoặc hết hạn giữ.
/// </summary>
[ApiController]
[Route("api/reviewer/source-videos")]
[Tags(ApiTags.SourceCorpus)]
[Authorize(Roles = "Reviewer")]
public class ReviewerSourceVideosController(ISourceVideoService sourceVideos) : ControllerBase
{
    /// <summary>Nhận video kế tiếp. 204 khi không còn video nào chờ duyệt.</summary>
    [HttpGet("next")]
    [ProducesResponseType(typeof(SourceVideoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<SourceVideoDto>> Next(CancellationToken ct)
    {
        var video = await sourceVideos.ClaimNextAsync(User.GetUserId(), ct);
        return video is null ? NoContent() : Ok(video);
    }

    /// <summary>Ghi đè toàn bộ danh sách câu của video đang giữ.</summary>
    [HttpPut("{id:long}/segments")]
    [ProducesResponseType(typeof(SourceVideoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<SourceVideoDto> SaveSegments(
        long id, [FromBody] SaveSourceSegmentsRequest request, CancellationToken ct)
    {
        return await sourceVideos.SaveSegmentsAsync(id, User.GetUserId(), request, ct);
    }

    /// <summary>Cắt clip WAV từng câu và đánh dấu video đã duyệt.</summary>
    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(SourceVideoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<SourceVideoDto> Approve(long id, CancellationToken ct)
    {
        return await sourceVideos.ApproveAsync(id, User.GetUserId(), ct);
    }
}
