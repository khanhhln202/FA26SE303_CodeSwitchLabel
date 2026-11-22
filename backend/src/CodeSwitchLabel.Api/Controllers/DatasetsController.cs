using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>Dataset xuất dữ liệu cho Admin: gom bản duyệt đạt, phát hành, tải về.</summary>
[ApiController]
[Route("api/datasets")]
[Tags(ApiTags.Statistics)]
[Authorize(Roles = "Admin")]
public class DatasetsController(IDatasetService datasets) : ControllerBase
{
    /// <summary>Tạo dataset nháp từ bản duyệt đạt (chọn tay và/hoặc theo đợt).</summary>
    /// <remarks>
    /// Dataset mới tạo ở trạng thái nháp. Mỗi cặp câu phải đủ cả hai bản cs và vi trước khi phát hành.
    /// Trả 409 nếu tên dataset đã tồn tại, 422 nếu không có bản duyệt đạt nào phù hợp.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(CreateDatasetResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreateDatasetResult>> Create(
        [FromBody] CreateDatasetRequest request, CancellationToken ct)
    {
        var result = await datasets.CreateAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Dataset.DatasetId }, result);
    }

    /// <summary>Danh sách dataset, mới nhất trước.</summary>
    /// <remarks>
    /// `?sort=` nhận `datasetId`, `createdAt`, `status`, `datasetName`, `recordingCount`;
    /// `?dir=asc|desc` (mặc định `asc`). Bỏ trống `sort` thì ra mới nhất trước.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DatasetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DatasetDto>>> Search(
        [FromQuery] PageRequest request, CancellationToken ct)
        => Ok(await datasets.SearchAsync(request, ct));

    /// <summary>Chi tiết dataset, kèm các cặp câu còn thiếu một biến thể.</summary>
    /// <remarks>
    /// Dùng để Reviewer/Admin biết còn thiếu bản ghi nào để hoàn tất dataset. Trả 404 nếu dataset không tồn tại.
    /// </remarks>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(DatasetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DatasetDetailDto>> Get(long id, CancellationToken ct)
        => Ok(await datasets.GetAsync(id, ct));

    /// <summary>Phát hành dataset. Mỗi cặp câu phải đủ cả hai bản cs và vi.</summary>
    /// <remarks>
    /// Sau khi phát hành, dataset không được sửa nữa. Trả 409 nếu còn cặp thiếu biến thể
    /// hoặc dataset không ở trạng thái nháp.
    /// </remarks>
    [HttpPost("{id:long}/release")]
    [ProducesResponseType(typeof(DatasetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DatasetDto>> Release(
        long id, [FromBody] ReleaseDatasetRequest request, CancellationToken ct)
        => Ok(await datasets.ReleaseAsync(id, request, User.GetUserId(), ct));

    /// <summary>Lưu trữ dataset đã phát hành.</summary>
    /// <remarks>
    /// Chỉ dataset đã phát hành mới lưu trữ được. Dataset lưu trữ vẫn giữ dữ liệu, chỉ
    /// biến khỏi danh sách đang dùng.
    /// </remarks>
    [HttpPost("{id:long}/archive")]
    [ProducesResponseType(typeof(DatasetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DatasetDto>> Archive(long id, CancellationToken ct)
        => Ok(await datasets.ArchiveAsync(id, ct));

    /// <summary>Xoá dataset nháp. Dataset đã phát hành hoặc lưu trữ thì không xoá được.</summary>
    /// <remarks>
    /// Trả 409 nếu dataset đã phát hành hoặc đã lưu trữ. Việc xoá không khôi phục được.
    /// </remarks>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await datasets.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Tải thẳng ZIP dữ liệu đã duyệt theo bản ghi, Speaker hoặc theo đợt, không cần tạo dataset.</summary>
    /// <remarks>
    /// Gửi `recordingIds` để chỉ lấy đúng các bản đã chọn; nếu không thì gửi `speakerIds`,
    /// hoặc bỏ trống cả hai rồi gửi `waveId` để lấy cả đợt.
    /// Mỗi cặp một thư mục `speaker_{id}_{ngày Bangkok}` gồm `text/{scriptId}.json` (đúng schema nhập)
    /// và `audio/{scriptId}-cs.wav`, `audio/{scriptId}-vi.wav`. Cặp thiếu một bản hoặc thiếu audio bị bỏ.
    /// </remarks>
    [HttpPost("export")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExportSpeakers(
        [FromBody] ExportSpeakerDataRequest request, CancellationToken ct)
    {
        var file = await datasets.ExportSpeakersAsync(request, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Tải dataset đã phát hành (ZIP gồm manifest.json, metadata.csv và audio/ nếu includeAudio=true).</summary>
    /// <remarks>
    /// `?includeAudio=true` (mặc định): đóng gói cả file ghi âm vào thư mục `audio/`.
    /// `?includeAudio=false`: chỉ lấy metadata kèm link nghe tạm 15 phút.
    /// </remarks>
    [HttpGet("{id:long}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Download(
        long id, [FromQuery] bool includeAudio = true, CancellationToken ct = default)
    {
        var file = await datasets.DownloadAsync(id, includeAudio, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
