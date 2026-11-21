using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// Gửi một video YouTube vào corpus hội thoại. Chỉ ghi hàng đợi rồi trả về ngay;
/// việc tải và phiên âm do <see cref="BackgroundJobs.SourceVideoWorker"/> làm nền.
/// </summary>
[ApiController]
[Route("api/source-videos")]
[Tags(ApiTags.SourceCorpus)]
[Authorize(Roles = "Speaker,TaskManager,Admin")]
public class SourceVideosController(ISourceVideoService sourceVideos) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(SourceVideoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SourceVideoDto>> Submit(
        [FromBody] SubmitSourceVideoRequest request, CancellationToken ct)
    {
        var video = await sourceVideos.SubmitAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = video.SourceVideoId }, video);
    }

    /// <summary>Người gửi xem tiến độ; Admin xem được mọi video.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(SourceVideoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<SourceVideoDto> Get(long id, CancellationToken ct)
    {
        var isAdmin = User.IsInRole("Admin");
        return await sourceVideos.GetAsync(id, User.GetUserId(), isAdmin, ct);
    }
}
