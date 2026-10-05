using System.ComponentModel.DataAnnotations;
using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// Kho câu. Mỗi mục là một CẶP CÂU: bản chen tiếng Anh (cs) và bản thuần Việt tương đương (ve),
/// kèm nhãn [vi]/[en] đánh dấu từng đoạn ngôn ngữ.
/// </summary>
[ApiController]
[Route("api/scripts")]
[Tags(ApiTags.Scripts)]
[Authorize]
public class ScriptsController(IScriptService scriptService) : ControllerBase
{
    /// <summary>Tìm kiếm, lọc theo trạng thái và chủ đề, có phân trang.</summary>
    /// <remarks>Từ khoá tìm trên cả hai câu của cặp.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ScriptListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ScriptListItemDto>>> Search(
        [FromQuery] ScriptSearchRequest request, CancellationToken ct)
        => Ok(await scriptService.SearchAsync(request, ct));

    /// <summary>Chi tiết một cặp câu, kèm bản đã bỏ nhãn, alignment và lịch sử duyệt nội dung.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScriptDetailDto>> Get(string id, CancellationToken ct)
        => Ok(await scriptService.GetAsync(id, ct));

    /// <summary>Admin thêm tay một cặp câu — vào thẳng trạng thái đã duyệt.</summary>
    /// <remarks>
    /// Cả hai câu phải có nhãn ngôn ngữ, ví dụ:
    /// `[vi]Em nên [en]scan [vi]tài liệu này` và `[vi]Em nên quét tài liệu này`.
    ///
    /// Hệ thống đếm số từ tiếng Anh từ nhãn — con số này đi vào **chữ số đầu của mã câu**
    /// nên phải nằm trong khoảng 1..9. Câu thuần Việt không được có nhãn [en] nào.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScriptDetailDto>> Create(
        [FromBody] CreateScriptRequest request, CancellationToken ct)
    {
        var created = await scriptService.CreateAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = created.ScriptId }, created);
    }

    /// <summary>Nhập hàng loạt từ file input_text.json.</summary>
    /// <remarks>
    /// Nhận cả file một câu lẫn file nhiều câu. Mỗi phần tử gồm `id`, `domain`, `cs_transcript`,
    /// `vi_equivalent` và `alignment`.
    ///
    /// Mã câu **được sinh lại** theo đúng quy tắc của database: giữ ý nghĩa ba chữ số đầu
    /// (số từ tiếng Anh, chủ đề, quan hệ Anh–Việt) rồi cấp số thứ tự mới.
    ///
    /// Câu hỏng **không làm hỏng cả file**: phần hợp lệ vẫn được nhập, phần bị loại nằm trong
    /// `skipped` kèm lý do. Câu nhập vào ở trạng thái **chờ duyệt nội dung**.
    /// </remarks>
    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ImportResultDto>> Import(
        [FromForm] ImportScriptsForm form, CancellationToken ct)
    {
        var ext = Path.GetExtension(form.File!.FileName ?? string.Empty).ToLowerInvariant();
        if (ext is not (".json" or ""))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = $"File {form.File.FileName} không phải JSON.",
                Type = "https://codeswitchlabel.local/errors/invalid_file_type",
                Extensions = { ["code"] = "invalid_file_type" }
            });
        }

        if (form.File.Length == 0)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "File rỗng, không có gì để nhập.",
                Type = "https://codeswitchlabel.local/errors/empty_file",
                Extensions = { ["code"] = "empty_file" }
            });
        }

        await using var stream = form.File!.OpenReadStream();

        return Ok(await scriptService.ImportAsync(stream, form.File.FileName, User.GetUserId(), ct));
    }

    /// <summary>Danh sách batch đã nhập, mới nhất trước.</summary>
    [HttpGet("import/batches")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PagedResult<ImportBatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ImportBatchDto>>> ListBatches(
        [FromQuery] PageRequest request, CancellationToken ct)
        => Ok(await scriptService.ListBatchesAsync(request, ct));

    /// <summary>Chi tiết một batch: file, số câu nhập được, danh sách mã câu.</summary>
    [HttpGet("import/batches/{batchId:long}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ImportBatchDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportBatchDetailDto>> GetBatch(long batchId, CancellationToken ct)
        => Ok(await scriptService.GetBatchAsync(batchId, ct));

    /// <summary>Duyệt nội dung một cặp câu: chấp nhận, sửa, hoặc từ chối.</summary>
    /// <remarks>
    /// Đây là use case "Review Text". Mỗi lượt duyệt ghi thành một dòng lịch sử riêng.
    ///
    /// - **Edited** — bắt buộc gửi **cả hai** câu đã sửa; lược đồ chỉ cho sửa theo cặp.
    ///   Số từ tiếng Anh không được đổi vì nó nằm trong mã câu.
    /// - **Rejected** — bắt buộc `errorReasonCode` lấy từ `GET /api/script-error-reasons`.
    ///   Câu bị loại thì mọi task đang chờ thu câu đó cũng mất mục ấy.
    ///
    /// Cặp câu đã có bản ghi âm thì **không sửa nội dung được nữa**.
    /// </remarks>
    [HttpPost("{id}/review")]
    [Authorize(Roles = "Admin,Speaker,Reviewer")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScriptDetailDto>> Review(
        string id, [FromBody] ReviewScriptRequest request, CancellationToken ct)
        => Ok(await scriptService.ReviewAsync(id, User.GetUserId(), request, ct));
}

public class ImportScriptsForm
{
    [Required(ErrorMessage = "Chưa đính kèm file JSON.")]
    public IFormFile? File { get; set; }
}
