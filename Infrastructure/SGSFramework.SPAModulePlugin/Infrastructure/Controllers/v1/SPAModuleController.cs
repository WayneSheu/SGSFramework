// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Presentation/Controllers/v1/SPAModuleController.cs
namespace SGSFramework.SPAModulePlugin.Presentation.Controllers.v1;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.SPAModulePlugin.Application.Commands;
using SGSFramework.SPAModulePlugin.Application.DTOs;
using SGSFramework.SPAModulePlugin.Application.Queries;
using SGSFramework.SPAModulePlugin.Domain.Enums;
using SGSFramework.SPAModulePlugin.Domain.ValueObjects;
using System.Net.Mime;
using System.Security.Claims;

/// <summary>
/// 前端 SPA 動態模組維護、部署與熱卸載管理控制器
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/spa-modules")]
[ControllerTitle("SPA模組管理", Icon = "fa-solid fa-cubes", Order = 100, Description = "前端 SPA 模組維護與動態載入卸載管理控制器")]
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

    /// <summary>
    /// 取得目前登入使用者授權的 SPA 動態外掛模組清單
    /// </summary>
    /// <param name="frameworkType">前端框架類型 (預設 Vue3)</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>授權 SPA 模組 Manifest 列表</returns>
    [HttpGet("manifest", Name = "GetAuthorizedSPAModules")]
    [Function("manifest", "前端SPA模組清單", Icon = "fa-solid fa-list-check", Order = 1, Description = "取得目前登入使用者授權的 SPA 動態外掛模組清單", IsMenu = true)]
    [RequiresPermission("SYSTEM.SPAMODULE.READ")]
    [ProducesResponseType(typeof(IReadOnlyList<SPAModuleManifest>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetManifest(
        [FromQuery] SPAFrameworkType frameworkType = SPAFrameworkType.Vue3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized("無效的使用者身分 Token。");
            }

            if (frameworkType == 0)
            {
                frameworkType = SPAFrameworkType.Vue3;
            }

            var query = new GetAuthorizedSPAModulesQuery(userId, frameworkType);
            var result = await _sender.Send(query, cancellationToken).ConfigureAwait(false);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 SPA 模組 Manifest 時發生系統例外。 Path: {Path}", HttpContext.Request.Path);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "系統內部錯誤",
                Detail = $"讀取 SPA 外掛清單失敗：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 上傳並部署新的 SPA 外掛模組套件 (.zip 或單一資產)
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Function("UploadSPAModule", "上傳SPA模組", Icon = "fa-solid fa-cloud-arrow-up", Order = 2, Description = "上傳並部署新的 SPA 外掛模組套件並完成動態掛載")]
    [RequiresPermission("SYSTEM.SPAMODULE.UPLOADMODULE", "上傳SPA模組")]
    [ProducesResponseType(typeof(SPAModuleUploadResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadModuleAsync(
        [FromForm] SPAModuleUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. 取得上傳檔案（優先從 Model Binding 取，若無則從 HttpContext.Request.Form.Files 自動救援）
        var uploadFiles = request?.Files?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();

        if (uploadFiles.Count == 0 && HttpContext.Request.HasFormContentType)
        {
            uploadFiles = HttpContext.Request.Form.Files.Where(f => f.Length > 0).ToList();
        }

        if (uploadFiles.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "請求參數無效",
                Detail = "上傳的 SPA 模組檔案不可為空。",
                Instance = HttpContext.Request.Path
            });
        }

        // 2. 解析 FrameworkType (預設 Vue3)
        var frameworkType = request?.FrameworkType ?? SPAFrameworkType.Vue3;

        try
        {
            var command = new UploadSPAModuleCommand(frameworkType, uploadFiles);
            var response = await _sender.Send(command, cancellationToken).ConfigureAwait(false);

            return CreatedAtRoute("GetAuthorizedSPAModules", new { frameworkType }, response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "上傳 SPA 模組時參數無效。 Path: {Path}", HttpContext.Request.Path);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "請求參數無效",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "上傳 SPA 模組檔案格式或 Zip Slip 安全驗證失敗。 Path: {Path}", HttpContext.Request.Path);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "檔案格式或安全驗證失敗",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上傳 SPA 模組時發生系統異常。 Path: {Path}", HttpContext.Request.Path);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "檔案處理過程發生錯誤",
                Detail = $"上傳 SPA 模組時發生系統異常：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 線上動態卸載並刪除指定 SPA 模組及其實體目錄
    /// </summary>
    /// <param name="moduleName">欲卸載的 SPA 模組名稱</param>
    /// <param name="frameworkType">前端框架類型 (預設 Vue3)</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>無內容成功回應</returns>
    [HttpDelete("{moduleName}")]
    [Function("RemoveSPAModule", "卸載SPA模組", Icon = "fa-solid fa-trash-can", Order = 3, Description = "線上動態卸載指定 SPA 模組並同步清除靜態目錄與資料庫元資料")]
    [RequiresPermission("SYSTEM.SPAMODULE.REMOVEMODULE", "卸載SPA模組")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveModuleAsync(
        string moduleName,
        [FromQuery] SPAFrameworkType frameworkType = SPAFrameworkType.Vue3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "請求參數無效",
                Detail = "模組名稱不可為空。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var command = new RemoveSPAModuleCommand(moduleName, frameworkType);
            var result = await _sender.Send(command, cancellationToken).ConfigureAwait(false);

            if (!result)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "找不到模組目錄",
                    Detail = $"找不到名為 [{moduleName}] 的 SPA 模組實體目錄。",
                    Instance = HttpContext.Request.Path
                });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "卸載 SPA 模組 [{ModuleName}] 時發生系統異常。", moduleName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "卸載 SPA 模組失敗",
                Detail = $"卸載並清除 SPA 模組 [{moduleName}] 時發生系統異常：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
    }
}