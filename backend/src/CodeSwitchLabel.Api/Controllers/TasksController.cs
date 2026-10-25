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
/// - **Recording** — danh sách cặp câu (script) giao cho một hoặc nhiều Speaker thu âm. Đếm theo cặp: đủ cả hai bản cs + vi đạt mới tính.
/// - **Review** — danh sách bản ghi chờ duyệt giao cho một hoặc nhiều Reviewer. Đếm theo bản ghi. Mỗi bản ghi
///   duyệt mù tối đa 3 lượt của 3 người khác nhau và chốt ngay khi một bên đủ 2 phiếu, nên mục không gắn với "vòng 1".
///   Task duyệt cũng là nơi chia lô câu văn bản cho từng Reviewer duyệt nội dung (`text_review.batch_size`).
///
/// Trạng thái **tự chạy theo công việc thật**: đạt chỉ tiêu thì Completed, chưa đạt thì
/// InProgress, đã huỷ thì giữ nguyên.
///
/// còn đúng ba trạng thái — `InProgress`, `Completed`, `Cancelled`. Trước đây có
/// thêm `Draft` và `Open`, nhưng hai trạng thái đó chỉ nói về việc giao chứ không nói về việc
/// làm. Task Manager vẫn đổi trạng thái bằng tay được qua `PATCH /api/tasks/{id}`; hệ thống
/// không ghi đè ngay, chỉ tính lại khi chỉ tiêu hoặc việc nộp/duyệt thật sự thay đổi.
/// </remarks>
[ApiController]
[Route("api/tasks")]
[Tags(ApiTags.Tasks)]
[Authorize(Roles = "TaskManager,Admin")]
public class TasksController(ITaskService taskService) : ControllerBase
{
    /// <summary>Tạo task mới — ở trạng thái InProgress. campaignId để trống = task độc lập, gắn sau.</summary>
    /// <remarks>
    /// Task gắn chiến dịch thì hạn hoàn thành phải nằm trong khoảng ngày của chiến dịch;
    /// task độc lập bỏ qua kiểm tra này. Hạn gửi kèm múi giờ nào cũng được, ví dụ
    /// <c>2026-09-30T17:00:00+07:00</c>; backend đổi sang UTC trước khi lưu.
    /// Lỗi trigger do race (vượt quota/khung hạn) cũng trả mã
    /// <c>task_target_exceeds_campaign</c> / <c>task_deadline_outside_campaign</c> (422).
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
    ///
    /// `?sort=` nhận `taskId`, `deadline`, `status`, `taskType`, `createdAt`, `targetQty`;
    /// `?dir=asc|desc` (mặc định `asc`). Bỏ trống `sort` thì ra hạn gần nhất trước, task không đặt hạn ở cuối —
    /// sắp theo `deadline` cũng giữ nguyên quy tắc đó ở cả hai chiều.
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

    /// <summary>Sửa mô tả, chỉ tiêu, hạn, hoặc đổi trạng thái bằng tay. Trường nào để trống thì giữ nguyên.</summary>
    /// <remarks>
    /// Task đã giao thì chỉ tiêu không được vượt số mục còn làm được.
    /// Nâng chỉ tiêu của task đã Completed thì task **tự mở lại**; hạ chỉ tiêu có thể làm task xong ngay.
    /// Task **chưa có người nhận** thì cứ đặt chỉ tiêu trước, thêm mục vào sau — cùng luật
    /// cũ khi còn trạng thái `Draft`, nay hỏi bằng "đã giao cho ai chưa" thay vì hỏi trạng thái.
    ///
    /// gửi kèm `status` để Task Manager đổi trạng thái bằng tay —
    /// `InProgress` (mở lại task đã xong), `Completed` (chốt sớm) hoặc `Cancelled` (dừng).
    /// Chọn `Cancelled` thì mọi lượt giao đang hoạt động cũng chuyển sang `cancelled`, y hệt nút Huỷ.
    /// Giá trị bấm tay được giữ tới khi có việc thật đổi chỉ tiêu hoặc nộp bản ghi/duyệt —
    /// lúc đó hệ thống tính lại và có thể ghi đè. Sửa mô tả, hạn hay chủ đề không đụng tới nó.
    /// </remarks>
    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDetailDto>> Update(
        long id, [FromBody] UpdateTaskRequest request, CancellationToken ct)
        => Ok(await taskService.UpdateAsync(id, request, ct, User.GetUserId(), User.IsInRole("Admin")));

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
    /// Tự lấp cho task thu âm lấy theo `script_id` (cũ nhất trước), lọc theo chủ đề nếu có.
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
        => Ok(await taskService.AddItemsAsync(id, request, ct, User.GetUserId(), User.IsInRole("Admin")));

    /// <summary>Gỡ một mục chưa làm khỏi task.</summary>
    /// <remarks>
    /// Chỉ gỡ được mục còn chờ trong task chưa hoàn thành. Mục đã xong hay đã bị bỏ qua thì giữ lại để còn dấu vết công việc.
    /// </remarks>
    [HttpDelete("{id:long}/items/{itemId}")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskDetailDto>> RemoveItem(long id, string itemId, CancellationToken ct)
        => Ok(await taskService.RemoveItemAsync(id, itemId, ct, User.GetUserId(), User.IsInRole("Admin")));

    /// <summary>Giao task cho một hoặc nhiều người.</summary>
    /// <remarks>
    /// body `{ "userIds": [6, 7] }` — một task giao được cho NHIỀU người
    /// cùng lúc. Người đang nhận không bị đuổi, lượt giao của họ giữ nguyên `active`.
    /// Trước đây giao là điều phối lại: lượt giao cũ chuyển sang `reassigned`.
    ///
    /// Chỉ tiêu là của CẢ NHÓM: task chỉ tiêu 100 giao cho 3 người vẫn là 100 câu,
    /// backend chia quota lần lượt 34/33/33. Người đã có mặt trong `userIds` bị bỏ qua
    /// chứ không báo lỗi, nhưng quota của cả nhóm được chia lại khi có người mới.
    ///
    /// giao task không còn làm đổi trạng thái nữa — task đã là `InProgress` ngay từ
    /// lúc tạo. Trạng thái chỉ đổi khi đạt chỉ tiêu, khi bị huỷ, hoặc khi Task Manager bấm tay.
    /// </remarks>
    [HttpPost("{id:long}/assign")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDetailDto>> Assign(
        long id, [FromBody] AssignTaskRequest request, CancellationToken ct)
        => Ok(await taskService.AssignAsync(id, [.. request.UserIds], ct, User.GetUserId(), User.IsInRole("Admin")));

    /// <summary>Thay toàn bộ người nhận đang hoạt động và đặt quota từng người.</summary>
    /// <remarks>
    /// Body: `{ "assignments": [{ "userId": 6, "targetQty": 30 }, { "userId": 7 }] }`.
    /// Người không còn trong danh sách chuyển sang `reassigned` (giữ lịch sử); người mới được thêm.
    ///
    /// `targetQty` để trống thì backend chia đều phần chỉ tiêu còn lại sau khi trừ các quota
    /// đã gửi; điền đủ tất cả thì tổng phải bằng đúng chỉ tiêu task. Mỗi người ít nhất 1.
    /// Với task thu âm và task duyệt ngoài đợt, tổng quota cũng không được vượt số mục người
    /// nhận làm được.
    /// </remarks>
    [HttpPut("{id:long}/assignments")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDetailDto>> SetAssignments(
        long id, [FromBody] SetTaskAssignmentsRequest request, CancellationToken ct)
        => Ok(await taskService.SetAssignmentsAsync(
            id, request, ct, User.GetUserId(), User.IsInRole("Admin")));

    /// <summary>gỡ một người khỏi task.</summary>
    /// <remarks>
    /// Lượt giao chuyển sang `reassigned` chứ không bị xoá, nên vẫn xem được ai từng nhận.
    /// Không gỡ được người nhận cuối cùng khi task đã có việc — làm vậy thì không ai làm
    /// tiếp được, dữ liệu đã làm thì còn nguyên nhưng không ai dùng. Muốn dừng hẳn thì dùng Huỷ.
    /// </remarks>
    [HttpDelete("{id:long}/assign/{userId:long}")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskDetailDto>> Unassign(long id, long userId, CancellationToken ct)
        => Ok(await taskService.UnassignAsync(id, userId, ct, User.GetUserId(), User.IsInRole("Admin")));

    /// <summary>Gắn task vào chiến dịch (attach), chuyển giữa các chiến dịch (move), hoặc gỡ ra (detach khi campaignId null).</summary>
    /// <remarks>
    /// Task độc lập chỉ người tạo hoặc Admin được gắn; task đang gắn thì phải là Task Manager
    /// sở hữu chiến dịch cũ (hoặc Admin). Gắn vào chiến dịch kiểm tra quota + khung hạn như lúc tạo.
    /// </remarks>
    [HttpPatch("{id:long}/campaign")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDetailDto>> Attach(
        long id, [FromBody] AttachTaskRequest request, CancellationToken ct)
        => Ok(await taskService.AttachAsync(id, request.CampaignId, ct, User.GetUserId(), User.IsInRole("Admin")));

    /// <summary>Huỷ task.</summary>
    /// <remarks>
    /// nút này và `PATCH /api/tasks/{id}` với `status = "Cancelled"` là CÙNG một việc
    /// — cả hai gọi chung một hàm, nên không thể xảy ra tình trạng một đường huỷ lượt giao còn
    /// đường kia không.
    ///
    /// Task đã huỷ không mở lại được và không sửa được nữa. Các mục giữ nguyên để còn dấu vết,
    /// nhưng không còn giữ chỗ — bản ghi trong đó lại được thêm vào task khác.
    /// Task đã Completed thì không huỷ được (dùng `status = "InProgress"` để mở lại rồi huỷ).
    /// </remarks>
    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskDetailDto>> Cancel(long id, CancellationToken ct)
        => Ok(await taskService.CancelAsync(id, ct, User.GetUserId(), User.IsInRole("Admin")));

    // Xoá cứng nhiệm vụ đã gỡ khỏi đợt (chưa có dữ liệu nộp thật)
    /// <summary>Xoá nhiệm vụ. Chỉ xoá được task đã gỡ khỏi đợt và chưa có dữ liệu nộp/duyệt thật.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await taskService.DeleteAsync(id, ct, User.GetUserId(), User.IsInRole("Admin"));
        return NoContent();
    }

    /// <summary>Những người giao được một loại task, kèm khối lượng đang nhận.</summary>
    /// <remarks>
    /// Chỉ trả người **đang hoạt động** và **đúng vai**:
    /// task thu âm là Speaker, task duyệt là Reviewer. Kèm số task, tổng quota đang chạy,
    /// email và số điện thoại để bảng phân công rộng hiển thị đủ cột.
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

    /// <summary>Tổng quan trang chủ Task Manager: chiến dịch phụ trách, tải từng người, task còn chạy.</summary>
    [HttpGet("/api/task-manager/overview")]
    [ProducesResponseType(typeof(TaskManagerOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskManagerOverviewDto>> Overview(CancellationToken ct)
        => Ok(await taskService.GetManagerOverviewAsync(User.GetUserId(), User.IsInRole("Admin"), ct));
}
