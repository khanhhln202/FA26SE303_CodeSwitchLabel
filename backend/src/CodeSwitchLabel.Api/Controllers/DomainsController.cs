using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>Admin quản lý chủ đề: tạo, đổi tên, xóa mềm.</summary>
[ApiController]
[Route("api/admin/domains")]
[Tags(ApiTags.SystemConfiguration)]
[Authorize(Roles = "Admin")]
public class DomainsController(IDomainService domainService) : ControllerBase
{
    /// <summary>Tạo chủ đề mới. Backend tự sinh `code` và chữ số trong mã câu.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TopicDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TopicDto>> Create(
        [FromBody] DomainNameRequest request, CancellationToken ct)
    {
        var created = await domainService.CreateAsync(request.Name, ct);
        return Created($"/api/admin/domains/{created.Code}", created);
    }

    /// <summary>Đổi tên hiển thị. `code` và chữ số trong mã câu không đổi.</summary>
    [HttpPut("{code}")]
    [ProducesResponseType(typeof(TopicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TopicDto>> Rename(
        string code, [FromBody] DomainNameRequest request, CancellationToken ct)
        => Ok(await domainService.RenameAsync(code, request.Name, ct));

    /// <summary>Xóa mềm: chủ đề biến khỏi danh sách chọn, dữ liệu cũ giữ nguyên code.</summary>
    [HttpDelete("{code}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        await domainService.DeleteAsync(code, ct);
        return NoContent();
    }
}
