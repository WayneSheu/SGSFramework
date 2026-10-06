namespace SGSFramework.SPAModulePlugin.Presentation.Controllers.v1;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.SPAModulePlugin.Application.Queries;
using SGSFramework.SPAModulePlugin.Domain.Enums;
using SGSFramework.SPAModulePlugin.Domain.ValueObjects;
using System.Net.Mime;
using System.Security.Claims;



/// <summary>
/// 前端SPA模組維護與動態載入管理控制器
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/spa-modules")]
[ControllerTitle("SPA模組管理", Icon = "fa-solid fa-cubes", Order = 100, Description = "前端SPA模組維護與動態載入管理控制器")]
[RequiresPermission("SYSTEM.SPAMODULE.READ")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public sealed class SPAModuleController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<SPAModuleController> _logger;

    public SPAModuleController(ISender sender, ILogger<SPAModuleController> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("manifest")]
    [Function("manifest", "前端SPA模組清單", Icon = "fa-solid fa-list-check", Order = 1, Description = "取得目前登入使用者授權的 SPA 動態外掛模組清單", IsMenu = true)]
    [RequiresPermission("SYSTEM.SPAMODULE.READ")]
    [ProducesResponseType(typeof(IReadOnlyList<SPAModuleManifest>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetManifest(
    [FromQuery] SPAFrameworkType frameworkType = SPAFrameworkType.Vue3, // 設為合法的預設值 (請依您 Enum 的實際名稱調整)
    CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized("無效的使用者身分 Token。");
            }

            // 防呆保護：若綁定結果仍為 0，可進行回退或改為預設值
            if (frameworkType == 0)
            {
                frameworkType = SPAFrameworkType.Vue3; // 或是您定義的合法列舉值
            }

            var query = new GetAuthorizedSPAModulesQuery(userId, frameworkType);
            var result = await _sender.Send(query, cancellationToken).ConfigureAwait(false);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 SPA 模組 Manifest 時發生系統例外");
            return StatusCode(StatusCodes.Status500InternalServerError, "讀取 SPA 外掛清單失敗。");
        }
    }
}