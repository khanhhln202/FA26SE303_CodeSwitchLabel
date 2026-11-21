using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>Theo dõi job tải video, thử lại job hỏng và tải ZIP corpus đã duyệt.</summary>
[ApiController]
[Route("api/admin")]
[Tags(ApiTags.SourceCorpus)]
[Authorize(Roles = "Admin")]
public class AdminSourceCorpusController(ISourceVideoService sourceVideos) : ControllerBase
{
    [HttpGet("source-videos")]
    [ProducesResponseType(typeof(PagedResult<SourceVideoListItem>), StatusCodes.Status200OK)]
    public async Task<PagedResult<SourceVideoListItem>> List(
        [FromQuery] PageRequest page,
        [FromQuery] SourceVideoStatus? status,
        CancellationToken ct)
    {
        return await sourceVideos.ListAsync(page, status, ct);
    }

    /// <summary>Đưa job Failed về hàng đợi và đặt lại số lần thử.</summary>
    [HttpPost("source-videos/{id:long}/retry")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(long id, CancellationToken ct)
    {
        await sourceVideos.RetryAsync(id, ct);
        return NoContent();
    }

    /// <summary>ZIP gồm manifest.jsonl và clip WAV của mọi video đã duyệt.</summary>
    [HttpGet("source-corpus/export")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var file = await sourceVideos.ExportAsync(ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
