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
    /// <summary>Tạo dataset nháp từ bản duyệt đạt (chọn tay và/hoặc theo chiến dịch).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateDatasetResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateDatasetResult>> Create(
        [FromBody] CreateDatasetRequest request, CancellationToken ct)
    {
        var result = await datasets.CreateAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Dataset.DatasetId }, result);
    }

    /// <summary>Danh sách dataset, mới nhất trước.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DatasetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DatasetDto>>> Search(
        [FromQuery] PageRequest request, CancellationToken ct)
        => Ok(await datasets.SearchAsync(request, ct));

    /// <summary>Chi tiết dataset, kèm các cặp câu còn thiếu một biến thể.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(DatasetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DatasetDetailDto>> Get(long id, CancellationToken ct)
        => Ok(await datasets.GetAsync(id, ct));

    /// <summary>Phát hành dataset. Mỗi cặp câu phải đủ cả hai bản cs và vi.</summary>
    [HttpPost("{id:long}/release")]
    [ProducesResponseType(typeof(DatasetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DatasetDto>> Release(
        long id, [FromBody] ReleaseDatasetRequest request, CancellationToken ct)
        => Ok(await datasets.ReleaseAsync(id, request, User.GetUserId(), ct));

    /// <summary>Lưu trữ dataset đã phát hành.</summary>
    [HttpPost("{id:long}/archive")]
    [ProducesResponseType(typeof(DatasetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DatasetDto>> Archive(long id, CancellationToken ct)
        => Ok(await datasets.ArchiveAsync(id, ct));

    /// <summary>Tải dataset đã phát hành (ZIP gồm manifest.json + metadata.csv, link nghe 15 phút).</summary>
    [HttpGet("{id:long}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Download(long id, CancellationToken ct)
    {
        var file = await datasets.DownloadAsync(id, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
