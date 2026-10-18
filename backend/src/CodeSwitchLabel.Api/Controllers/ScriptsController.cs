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
    /// <remarks>
    /// Từ khoá tìm trên cả hai câu của cặp.
    ///
    /// TEAM_012: `?sort=` nhận `scriptId`, `createdAt`, `domain`, `status`; `?dir=asc|desc`
    /// (mặc định `asc`). Bỏ trống `sort` thì ra mới nhất trước — tức `sort=createdAt` + `dir=desc`.
    /// </remarks>
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

    /// <summary>Admin thêm tay một cặp câu — vào thẳng trạng thái đã duyệt kèm lượt duyệt tự động.</summary>
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

    /// <summary>Đóng góp một cặp câu mới — dành cho Speaker.</summary>
    /// <remarks>
    /// Câu vào trạng thái **chờ duyệt nội dung**, khác với `POST /api/scripts` của Admin (vào thẳng đã duyệt).
    ///
    /// Trùng việc với `POST /api/speaker/scripts/contribute`; đường kia vẫn chạy nên frontend
    /// đang gọi nó không phải sửa.
    ///
    /// Reviewer **không** dùng đường này: người vừa viết câu vừa duyệt câu là xung đột vai, cùng
    /// lý do hệ thống cấm tự duyệt bản ghi của chính mình. Màn hình duyệt câu đóng góp của
    /// Reviewer nằm ở `GET /api/reviewer/contributions`.
    /// </remarks>
    [HttpPost("contribute")]
    [Authorize(Roles = "Speaker")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScriptDetailDto>> Contribute(
        [FromBody] CreateScriptRequest request, CancellationToken ct)
    {
        var created = await scriptService.ContributeAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = created.ScriptId }, created);
    }

    /// <summary>Admin sửa nội dung một cặp câu đang chờ duyệt — sửa bao nhiêu lần cũng được.</summary>
    /// <remarks>
    /// Khác `POST /api/scripts/{id}/review` với `action: Edited`: đường đó **sửa xong là duyệt luôn**,
    /// câu chuyển sang đã duyệt và không sửa tiếp được. Đường này chỉ đổi nội dung, câu **ở nguyên
    /// trạng thái chờ duyệt**, nên Admin đổi đi đổi lại tới khi ưng rồi mới chốt bằng `action: Accepted`.
    ///
    /// Ba thứ không đổi được vì chúng nằm trong mã câu: **số từ tiếng Anh**, **chủ đề** và
    /// **quan hệ Anh–Việt**. Muốn đổi thì loại câu này rồi tạo cặp mới.
    ///
    /// - **409 `script_not_pending`** — câu đã duyệt, đã bị loại hoặc đã ngừng dùng.
    /// - **409 `script_already_recorded`** — đã có bản ghi âm, sửa sẽ làm transcript lệch với âm thanh.
    /// - **422 `en_word_count_locked`** / **`relation_locked`** — bản sửa đổi thứ nằm trong mã câu.
    /// </remarks>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScriptDetailDto>> UpdatePending(
        string id, [FromBody] UpdateScriptContentRequest request, CancellationToken ct)
        => Ok(await scriptService.UpdatePendingAsync(id, request, ct));

    /// <summary>Nhập hàng loạt từ file input_text.json.</summary>
    /// <remarks>
    /// Nhận cả file một câu lẫn file nhiều câu. Mỗi phần tử gồm `id`, `domain`, `cs_transcript`,
    /// `vi_equivalent` và `alignment`.
    ///
    /// Mã câu **được sinh lại** theo đúng quy tắc của database: giữ ý nghĩa ba chữ số đầu
    /// (số từ tiếng Anh, chủ đề, quan hệ Anh–Việt) rồi cấp số thứ tự mới.
    ///
    /// Câu hỏng **không làm hỏng cả file**: phần hợp lệ vẫn được nhập, phần bị loại nằm trong
    /// `skipped` kèm lý do.
    ///
    /// Câu nhập vào nằm ở trạng thái **chờ duyệt nội dung**, KHÔNG tự động duyệt. Admin xem lại
    /// từng câu rồi chốt: `PUT /api/scripts/{id}` để sửa (sửa bao nhiêu lần cũng được),
    /// `POST /api/scripts/{id}/review` với `action: Rejected` để loại,
    /// hoặc `action: Accepted` để đưa sang đã duyệt — sau bước đó thì câu bị khoá, không sửa nữa.
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
        // [Required] trên form đã chặn trường hợp không đính kèm file. Tên file có thể rỗng khi client
        // gửi phần multipart không kèm filename, nên đặt sẵn một tên thay thế: cột file_name không nhận NULL.
        var file = form.File!;
        var fileName = string.IsNullOrWhiteSpace(file.FileName) ? "import.json" : file.FileName;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is not (".json" or ""))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = $"File {fileName} không phải JSON.",
                Type = "https://codeswitchlabel.local/errors/invalid_file_type",
                Extensions = { ["code"] = "invalid_file_type" }
            });
        }

        if (file.Length == 0)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "File rỗng, không có gì để nhập.",
                Type = "https://codeswitchlabel.local/errors/empty_file",
                Extensions = { ["code"] = "empty_file" }
            });
        }

        await using var stream = file.OpenReadStream();

        return Ok(await scriptService.ImportAsync(stream, fileName, User.GetUserId(), ct));
    }

    /// <summary>Danh sách batch đã nhập, mới nhất trước.</summary>
    /// <remarks>TEAM_012: `?sort=` nhận `batchId`, `createdAt`, `fileName`, `scriptCount`.</remarks>
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

    /// <summary>Khôi phục cặp câu đã bị loại về chờ duyệt.</summary>
    [HttpPost("{id}/restore")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ScriptDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScriptDetailDto>> Restore(string id, CancellationToken ct)
        => Ok(await scriptService.RestoreAsync(id, ct));
}

public class ImportScriptsForm
{
    [Required(ErrorMessage = "Chưa đính kèm file JSON.")]
    public IFormFile? File { get; set; }
}
