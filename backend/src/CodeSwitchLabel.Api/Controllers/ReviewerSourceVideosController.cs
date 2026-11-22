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
    /// <remarks>
    /// Mỗi Reviewer chỉ giữ được một video một lúc. Video bị khoá cho đến khi duyệt,
    /// bỏ qua hoặc hết hạn giữ (TTL). Trả 204 nếu không còn video nào.
    /// </remarks>
    [HttpGet("next")]
    [ProducesResponseType(typeof(SourceVideoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<SourceVideoDto>> Next(CancellationToken ct)
    {
        var video = await sourceVideos.ClaimNextAsync(User.GetUserId(), ct);
        return video is null ? NoContent() : Ok(video);
    }

    /// <summary>Ghi đè toàn bộ danh sách câu của video đang giữ.</summary>
    /// <remarks>
    /// Chỉ Reviewer đang giữ video đó mới gọi được. Đầu vào là toàn bộ danh sách segment,
    /// các segment không nằm trong danh sách sẽ bị xoá. Trả 409 nếu video không còn được giữ.
    /// </remarks>
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
    /// <remarks>
    /// Sau khi duyệt, video được gắn dấu hoàn tất và rút khỏi hàng đợi của Reviewer.
    /// Trả 409 nếu video đã được duyệt hoặc không còn được giữ.
    /// </remarks>
    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(typeof(SourceVideoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<SourceVideoDto> Approve(long id, CancellationToken ct)
    {
        return await sourceVideos.ApproveAsync(id, User.GetUserId(), ct);
    }
}
