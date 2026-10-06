using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Abstractions;
using CodeSwitchLabel.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSwitchLabel.Api.Controllers;

/// <summary>Tham số hệ thống cho mọi vai — bản đọc công khai của /api/admin/config.</summary>
/// <remarks>
/// Speaker/Reviewer cần `maxEnglishWords` và danh sách chủ đề để validate form
/// mà không phải là Admin. Ghi vẫn chỉ Admin qua `PUT /api/admin/config/{key}`.
/// </remarks>
[ApiController]
[Route("api/config")]
[Tags(ApiTags.SystemConfiguration)]
[Authorize]
public class ConfigController(ISystemConfigService configService) : ControllerBase
{
    /// <summary>Toàn bộ tham số hệ thống (đọc).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SystemConfigDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SystemConfigDto>>> GetAll(CancellationToken ct)
        => Ok(await configService.GetAllAsync(ct));

    /// <summary>Một tham số theo khoá.</summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(SystemConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SystemConfigDto>> Get(string key, CancellationToken ct)
        => Ok(await configService.GetAsync(key, ct));

    /// <summary>Danh sách chủ đề (domain) cố định của hệ thống.</summary>
    /// <remarks>FE dùng cho ô chọn chủ đề, thay cho `admin_topic_config_v2` local.</remarks>
    [HttpGet("topics")]
    [ProducesResponseType(typeof(IEnumerable<TopicDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TopicDto>> Topics()
        => Ok(new[]
        {
            new TopicDto(nameof(ScriptDomain.ItTechnology), "Công nghệ thông tin"),
            new TopicDto(nameof(ScriptDomain.Education), "Giáo dục"),
            new TopicDto(nameof(ScriptDomain.DailyLife), "Hội thoại hàng ngày"),
        });
}

public record TopicDto(string Code, string Name);
