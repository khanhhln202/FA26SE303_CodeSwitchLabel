using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>
/// Task Manager tạo task, chọn mục, giao cho người làm và theo dõi tiến độ.
/// </summary>
/// <remarks>
/// Hai loại task:
///
/// - **Recording** — danh sách script giao cho một Speaker thu âm.
/// - **Review** — danh sách bản ghi vòng 1 giao cho một Reviewer duyệt.
///
/// Trạng thái **tự chạy theo công việc thật**, không ai bấm tay — trừ Huỷ:
///
/// Draft (chưa giao) → Open (đã giao) → InProgress (có việc đầu tiên) → Completed (đạt chỉ tiêu).
/// </remarks>
[ApiController]
[Route("api/tasks")]
[Tags(ApiTags.Tasks)]
[Authorize(Roles = "TaskManager,Admin")]
public class TasksController(ITaskService taskService) : ControllerBase
{
    /// <summary>Tạo task mới — ở trạng thái Draft.</summary>
    /// <remarks>
    /// Task phải thuộc một chiến dịch đã có; hạn hoàn thành phải nằm trong khoảng ngày của chiến dịch.
    /// Hạn gửi kèm múi giờ nào cũng được, ví dụ <c>2026-09-30T17:00:00+07:00</c>;
    /// backend đổi sang UTC trước khi lưu.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "TaskManager")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDetailDto>> Create(
        [FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        var created = await taskService.CreateAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Summary.TaskId }, created);
    }

    /// <summary>Danh sách task, có lọc và phân trang.</summary>
    /// <remarks>
    /// Sắp theo hạn gần nhất trước. <c>overdue=true</c> chỉ lấy task **còn đang làm** mà đã quá hạn —
    /// task đã xong hay đã huỷ thì không tính là trễ.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TaskListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TaskListItemDto>>> Search(
        [FromQuery] TaskSearchRequest request, CancellationToken ct)
        => Ok(await taskService.SearchAsync(request, ct));

    /// <summary>Chi tiết task: tiến độ, toàn bộ lịch sử giao việc, và từng mục.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDetailDto>> Get(long id, CancellationToken ct)
        => Ok(await taskService.GetAsync(id, ct));

    /// <summary>Sửa mô tả, chỉ tiêu hoặc hạn. Trường nào để trống thì giữ nguyên.</summary>
    /// <remarks>
    /// Task đã giao thì chỉ tiêu không được vượt số mục còn làm được.
    /// Nâng chỉ tiêu của task đã Completed thì task **tự mở lại**; hạ chỉ tiêu có thể làm task xong ngay.
    /// </remarks>
    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDetailDto>> Update(
        long id, [FromBody] UpdateTaskRequest request, CancellationToken ct)
        => Ok(await taskService.UpdateAsync(id, request, ct));

    /// <summary>Thêm mục vào task — chọn tay, hoặc để hệ thống tự lấp.</summary>
    /// <remarks>
    /// Gửi **đúng một** trong hai:
    ///
    /// - <c>{ "ids": ["s_211000001"] }</c> — mã cặp câu với task thu âm, mã bản ghi với task duyệt.
    /// - <c>{ "autoFill": { "count": 10, "domain": "ItTechnology" } }</c> — task thu âm, lọc theo chủ đề.
    /// - <c>{ "autoFill": { "count": 10, "speakerId": 6 } }</c> — task duyệt, lọc theo người đọc.
    ///
    /// Mục không hợp lệ **không làm hỏng cả request**: phần hợp lệ vẫn được thêm,
    /// phần bị loại nằm trong <c>skipped</c> kèm lý do.
    ///
    /// Tự lấp cho task thu âm ưu tiên script có **ít giọng đã duyệt nhất**, để độ phủ trải đều.
    /// Task đã có người nhận thì tự lấp tránh script người đó đã thu, đã bỏ qua, hoặc đang có trong task khác.
    /// Tự lấp cho task duyệt lấy bản **cũ nhất trước**, không lấy bản do chính người nhận thu.
    /// </remarks>
    [HttpPost("{id:long}/items")]
    [ProducesResponseType(typeof(AddTaskItemsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AddTaskItemsResult>> AddItems(
        long id, [FromBody] AddTaskItemsRequest request, CancellationToken ct)
        => Ok(await taskService.AddItemsAsync(id, request, ct));

    /// <summary>Gỡ một mục chưa làm khỏi task.</summary>
    /// <remarks>
    /// Chỉ gỡ được mục còn chờ. Mục đã xong hay đã bị bỏ qua thì giữ lại để còn dấu vết công việc.
    /// </remarks>
    [HttpDelete("{id:long}/items/{itemId}")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskDetailDto>> RemoveItem(long id, string itemId, CancellationToken ct)
        => Ok(await taskService.RemoveItemAsync(id, itemId, ct));

    /// <summary>Giao task cho một người — hoặc chuyển sang người khác.</summary>
    /// <remarks>
    /// Mỗi task **đúng một người nhận** tại một thời điểm. Task thu âm giao cho Speaker,
    /// task duyệt giao cho Reviewer. Chỉ tiêu không được vượt số mục làm được.
    ///
    /// Giao cho người khác khi task đã có người nhận chính là **điều phối lại**: lượt giao cũ
    /// chuyển sang Reassigned chứ không bị xoá, nên vẫn xem được ai từng nhận task.
    /// Công việc người cũ đã làm vẫn tính cho task.
    /// </remarks>
    [HttpPost("{id:long}/assign")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDetailDto>> Assign(
        long id, [FromBody] AssignTaskRequest request, CancellationToken ct)
        => Ok(await taskService.AssignAsync(id, request.UserId!.Value, ct));

    /// <summary>Huỷ task — việc duy nhất phải bấm tay.</summary>
    /// <remarks>
    /// Task đã huỷ không mở lại được và không sửa được nữa. Các mục giữ nguyên để còn dấu vết,
    /// nhưng không còn giữ chỗ — bản ghi trong đó lại được thêm vào task khác.
    /// Task đã Completed thì không huỷ được.
    /// </remarks>
    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskDetailDto>> Cancel(long id, CancellationToken ct)
        => Ok(await taskService.CancelAsync(id, ct));

    /// <summary>Những người giao được một loại task, kèm khối lượng đang nhận.</summary>
    /// <remarks>
    /// Dùng cho ô chọn người nhận khi giao task. Chỉ trả người **đang hoạt động** và **đúng vai**:
    /// task thu âm là Speaker, task duyệt là Reviewer. Kèm số task và tổng chỉ tiêu đang chạy để chia việc cho đều —
    /// mỗi cặp câu chỉ một người đọc, nên chia lệch là dữ liệu lệch.
    ///
    /// Chỉ trả tên và vai, không lộ email, số điện thoại hay hồ sơ cá nhân.
    /// </remarks>
    [HttpGet("assignable-users")]
    [ProducesResponseType(typeof(IReadOnlyList<AssignableUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AssignableUserDto>>> AssignableUsers(
        [FromQuery, BindRequired] TaskType taskType, CancellationToken ct)
        => Ok(await taskService.GetAssignableUsersAsync(taskType, ct));

    /// <summary>Tổng hợp theo từng người đang nhận việc.</summary>
    /// <remarks>
    /// Mỗi người: số task đang làm, tổng chỉ tiêu, tổng đã xong, phần trăm, số task quá hạn.
    /// Dùng để Task Manager thấy ai đang quá tải hay chậm trước khi giao thêm.
    /// </remarks>
    [HttpGet("by-assignee")]
    [ProducesResponseType(typeof(IReadOnlyList<AssigneeSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AssigneeSummaryDto>>> ByAssignee(CancellationToken ct)
        => Ok(await taskService.GetAssigneeSummaryAsync(ct));
}
