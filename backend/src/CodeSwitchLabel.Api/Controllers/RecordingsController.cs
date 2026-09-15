using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

[ApiController]
[Route("api/recordings")]
[Tags("6 · Bản ghi âm")]
[Authorize]
public class RecordingsController(IRecordingService recordingService) : ControllerBase
{
    /// <summary>Nộp một bản ghi âm.</summary>
    /// <remarks>
    /// Gửi nguyên file trình duyệt ghi ra (WebM, OGG, MP4, WAV đều được) — **không chuyển định dạng ở frontend**.
    /// Backend tự chuyển sang WAV 16 kHz mono, đo thời lượng, kiểm tra tự động, rồi lưu vào kho.
    ///
    /// **201 Created với qcPassed = false** nghĩa là bản ghi đã được lưu nhưng trượt kiểm tra tự động
    /// (ví dụ quá ngắn) — nó sẽ không vào hàng đợi của Reviewer. Giao diện nên đọc danh sách
    /// qcIssues và mời người đọc thu lại.
    ///
    /// **409** nghĩa là bạn đã có bản ghi đang chờ duyệt hoặc đã được duyệt cho script này.
    /// Bản bị từ chối hay trượt kiểm tra thì vẫn thu lại được.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Speaker")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadRecordingResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UploadRecordingResult>> Upload(
        [FromForm] UploadRecordingForm form, CancellationToken ct)
    {
        await using var stream = form.Audio!.OpenReadStream();

        var result = await recordingService.UploadAsync(
            new UploadRecordingCommand(
                User.GetUserId(),
                form.ScriptId!.Value,
                form.TaskId,
                stream,
                form.Audio.FileName,
                form.Audio.Length),
            ct);

        return CreatedAtAction(nameof(Get), new { id = result.Recording.RecordingId }, result);
    }

    /// <summary>Lịch sử bản ghi của chính mình.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Speaker")]
    [ProducesResponseType(typeof(PagedResult<RecordingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RecordingDto>>> Mine(
        [FromQuery] RecordingSearchRequest request, CancellationToken ct)
        => Ok(await recordingService.GetMineAsync(User.GetUserId(), request, ct));

    /// <summary>Thông tin một bản ghi.</summary>
    /// <remarks>Speaker chỉ xem được bản của mình; Reviewer, Task Manager, Admin xem được mọi bản.</remarks>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(RecordingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecordingDto>> Get(long id, CancellationToken ct)
        => Ok(await recordingService.GetAsync(id, OwnerFilter(), ct));

    /// <summary>Lấy link nghe tạm thời.</summary>
    /// <remarks>
    /// Link tự hết hạn sau số phút cấu hình. Trình duyệt tải thẳng từ kho lưu trữ,
    /// API không làm trung gian truyền file. Dùng thẳng làm src của thẻ audio.
    /// </remarks>
    [HttpGet("{id:long}/audio-url")]
    [ProducesResponseType(typeof(AudioUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AudioUrlDto>> AudioUrl(long id, CancellationToken ct)
        => Ok(await recordingService.GetAudioUrlAsync(id, OwnerFilter(), ct));

    /// <summary>Speaker chỉ được đụng tới bản ghi của mình; các vai khác thì không giới hạn.</summary>
    private long? OwnerFilter() => User.IsInRole("Speaker") ? User.GetUserId() : null;
}

public class UploadRecordingForm
{
    [Required(ErrorMessage = "Chưa đính kèm file âm thanh.")]
    public IFormFile? Audio { get; set; }

    [Required(ErrorMessage = "Thiếu scriptId.")]
    public long? ScriptId { get; set; }

    /// <summary>Để trống nếu thu tự do, không thuộc task nào.</summary>
    public long? TaskId { get; set; }
}
