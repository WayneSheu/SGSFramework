using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.DTOs;

namespace SGSFramework.ModulePlugin.Controllers.v1;

/// <summary>
/// 商業模組維護與動態載入管理控制器
/// </summary>
[ApiController]
[Route("api/v1/system/modules")]
[Produces("application/json")]
[Menu("商業模組管理", "fa-solid fa-flask", order: 10, parent: null)]
[RequiresPermission("MODULEMANAGEMENT_READ")]
[Description("商業模組維護與動態載入管理")]
[Order(10)]
public class ModuleManagementController : ApiControllerBase
{
    private readonly IModuleManagementApplicationService _moduleAppService;
    private readonly ILogger<ModuleManagementController> _logger;

    public ModuleManagementController(
        IModuleManagementApplicationService moduleAppService,
        ILogger<ModuleManagementController> logger)
    {
        _moduleAppService = moduleAppService ?? throw new ArgumentNullException(nameof(moduleAppService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 查詢目前所有已載入掛載的商務模組完整資訊
    /// </summary>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>模組詳細資訊列表</returns>
    [HttpGet(Name = "GetActiveModulesDetails")]
    [Menu("查詢已載入模組詳情", "fa-solid fa-list-check", order: 1, parent: "商業模組管理")]
    [RequiresPermission("MODULEMANAGEMENT_GETACTIVEMODULESDETAILS")]
    [Order(1)]
    [Description("查詢目前所有已載入掛載的商務模組完整資訊")]
    [ProducesResponseType(typeof(IEnumerable<ModuleDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<ModuleDetailResponse>>> GetActiveModulesDetailsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _moduleAppService.GetActiveModulesDetailsAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢已載入模組詳情時發生系統異常。 Path: {Path}", HttpContext.Request.Path);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "系統內部錯誤",
                Detail = $"查詢已載入模組詳情時發生系統異常：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 上傳並儲存新的模組檔案
    /// </summary>
    /// <remarks>支援單一 DLL 或多檔案批次上傳，自動解除檔案鎖定、更新並同步註冊控制器元資料。</remarks>
    /// <param name="files">上傳的模組相關檔案列表</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>模組上傳結果資訊</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Menu("上傳模組", "fa-solid fa-upload", order: 3, parent: "商業模組管理")]
    [RequiresPermission("MODULEMANAGEMENT_UPLOADMODULE")]
    [Order(4)]
    [Description("上傳並儲存新的模組檔案")]
    [ProducesResponseType(typeof(ModuleUploadResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadModuleAsync([FromForm] List<IFormFile> files, CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "請求參數無效",
                Detail = "上傳的檔案不可為空。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var response = await _moduleAppService.UploadModuleAsync(files, cancellationToken);

            // 明確指定 RouteName 回傳 201 Created，確保 Location Header 成功計算並防止 Dynamic Controller 尋路例外
            return CreatedAtRoute("GetActiveModulesDetails", null, response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "上傳模組時參數無效。 Path: {Path}", HttpContext.Request.Path);
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
            _logger.LogWarning(ex, "上傳模組檔案格式或識別失敗。 Path: {Path}", HttpContext.Request.Path);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "檔案格式不正確",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上傳模組時發生系統異常。 Path: {Path}", HttpContext.Request.Path);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "檔案處理過程發生錯誤",
                Detail = $"上傳模組時發生系統異常：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 切換或更新指定模組的啟用狀態
    /// </summary>
    /// <param name="moduleName">模組名稱</param>
    /// <param name="request">啟用狀態變更請求</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>切換後狀態</returns>
    [HttpPatch("{moduleName}/status")]
    [Menu("切換模組狀態", "fa-solid fa-toggle-on", order: 3, parent: "商業模組管理")]
    [RequiresPermission("MODULEMANAGEMENT_TOGGLESTATUS")]
    [Order(2)]
    [Description("切換或更新指定模組的啟用狀態")]
    [ProducesResponseType(typeof(ToggleStatusResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleStatusAsync(
        string moduleName,
        [FromBody] ToggleStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleName) || request == null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "請求參數無效",
                Detail = "模組名稱與變更請求不可為空。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var result = await _moduleAppService.ToggleModuleStatusAsync(moduleName, request.IsActive, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "切換模組狀態失敗，找不到對應模組 [{ModuleName}]。", moduleName);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "找不到資源",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "切換模組狀態失敗，找不到對應 DLL 檔案。");
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "啟用模組失敗",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切換模組 [{ModuleName}] 狀態時發生異常。", moduleName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "系統內部錯誤",
                Detail = $"切換模組狀態時發生系統異常：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 線上動態卸載並同步清除指定模組與其對應的控制器元資料
    /// </summary>
    /// <param name="moduleName">欲卸載的模組名稱</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpDelete("{moduleName}")]
    [Menu("卸載模組", "fa-solid fa-trash", order: 4, parent: "商業模組管理")]
    [RequiresPermission("MODULEMANAGEMENT_REMOVEMODULE")]
    [Order(3)]
    [Description("線上動態卸載指定模組並同步清除資料庫中的控制器與模組元資料")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveModuleAsync(string moduleName, CancellationToken cancellationToken = default)
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
            await _moduleAppService.RemoveModuleAsync(moduleName, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "卸載模組 [{ModuleName}] 時發生系統異常。", moduleName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "卸載模組失敗",
                Detail = $"卸載並同步模組 [{moduleName}] 時發生系統異常：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
    }
}