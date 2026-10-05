using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

[ApiController]
[Route("api/recordings")]
[Tags(ApiTags.Recordings)]
[Authorize]
public class RecordingsController(IRecordingService recordingService) : ControllerBase
{
    /// <summary>Nộp một bản ghi âm cho một biến thể của cặp câu.</summary>
    /// <remarks>
    /// `sentenceVariant` chọn bạn đang đọc bản nào:
    /// **CodeSwitching** là câu chen tiếng Anh, **PureVietnamese** là câu thuần Việt.
    /// Mỗi cặp câu cần đủ cả hai, và phải do cùng một người đọc.
    ///
    /// Gửi nguyên file trình duyệt ghi ra (WebM, OGG, MP4, WAV đều được) — **không chuyển định dạng
    /// ở frontend**. Backend tự chuyển sang WAV 16 kHz mono, đo thời lượng, kiểm tra tự động rồi lưu.
    ///
    /// Mã bản ghi sinh theo mã câu: `r_cs_...` hoặc `r_vi_...`, thu lại lần hai trở đi thêm hậu tố `_t2`.
    ///
    /// **201 kèm qcPassed = false** nghĩa là bản ghi đã lưu nhưng trượt kiểm tra tự động, và
    /// sẽ không vào hàng đợi của Reviewer.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Speaker")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
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
                form.ScriptId!,
                form.SentenceVariant!.Value,
                form.TaskId,
                stream,
                form.Audio.FileName,
                form.Audio.Length),
            ct);

        return CreatedAtAction(nameof(Get), new { id = result.Recording.RecordingId }, result);
    }

    /// <summary>Danh sách bản ghi, có lọc và phân trang.</summary>
    /// <remarks>Speaker chỉ thấy bản của chính mình; Reviewer, Task Manager và Admin thấy tất cả.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RecordingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RecordingDto>>> Search(
        [FromQuery] RecordingSearchRequest request, CancellationToken ct)
        => Ok(await recordingService.SearchAsync(request, OwnerFilter(), ct));

    /// <summary>Thông tin một bản ghi.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RecordingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecordingDto>> Get(string id, CancellationToken ct)
        => Ok(await recordingService.GetAsync(id, OwnerFilter(), ct));

    /// <summary>Lấy link nghe tạm thời.</summary>
    /// <remarks>
    /// Cột cloud_link trong database là địa chỉ cố định của file và không mở trực tiếp được.
    /// Endpoint này ký một link có hạn; trình duyệt tải thẳng từ kho lưu trữ, API không truyền file.
    /// </remarks>
    [HttpGet("{id}/audio-url")]
    [ProducesResponseType(typeof(AudioUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AudioUrlDto>> AudioUrl(string id, CancellationToken ct)
        => Ok(await recordingService.GetAudioUrlAsync(id, OwnerFilter(), ct));

    /// <summary>Speaker chỉ được đụng tới bản ghi của mình; các vai khác thì không giới hạn.</summary>
    private long? OwnerFilter() => User.IsInRole("Speaker") ? User.GetUserId() : null;
}

public class UploadRecordingForm
{
    [Required(ErrorMessage = "Chưa đính kèm file âm thanh.")]
    public IFormFile? Audio { get; set; }

    /// <summary>Mã cặp câu, ví dụ s_211000001.</summary>
    [Required(ErrorMessage = "Thiếu scriptId.")]
    public string? ScriptId { get; set; }

    /// <summary>CodeSwitching hoặc PureVietnamese.</summary>
    [Required(ErrorMessage = "Phải chọn đang đọc bản nào: CodeSwitching hay PureVietnamese.")]
    public SentenceVariant? SentenceVariant { get; set; }

    /// <summary>Để trống nếu thu tự do, không thuộc task nào.</summary>
    public long? TaskId { get; set; }
}
