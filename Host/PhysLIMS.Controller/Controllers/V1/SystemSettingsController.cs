using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;

namespace SGSFramework.ApiInfrastructure.Controllers.V1;

/// <summary>
/// 系統配置管理控制器（外觀識別 Logo / 系統標題、Identity 密碼與帳號鎖定規則）
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/system-settings")]
[ControllerTitle("系統配置", Icon = "fa-solid fa-sliders", Order = 23, Description = "管理系統外觀識別（Logo、系統標題）與 Identity 密碼、帳號鎖定規則")]
[RequiresPermission("SYSTEM.SYSTEMSETTINGS.READ", "系統配置")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public sealed class SystemSettingsController : ApiControllerBase
{
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly ILogger<SystemSettingsController> _logger;

    public SystemSettingsController(
        ISystemSettingsService systemSettingsService,
        ILogger<SystemSettingsController> logger)
    {
        ArgumentNullException.ThrowIfNull(systemSettingsService);
        ArgumentNullException.ThrowIfNull(logger);

        _systemSettingsService = systemSettingsService;
        _logger = logger;
    }

    /// <summary>
    /// 取得完整系統配置（含 Identity 預設規則）
    /// </summary>
    [HttpGet]
    [Function("GetSystemSettings", "系統配置", Icon = "fa-solid fa-sliders", Order = 1, Description = "檢視與維護系統外觀識別與 Identity 安全規則", IsMenu = true, Path = "/sgsframework.system/systemsettings")]
    [RequiresPermission("SYSTEM.SYSTEMSETTINGS.READ")]
    [EndpointSummary("取得系統配置")]
    [EndpointDescription("取得外觀識別、目前生效的 Identity 規則與預設規則。")]
    [ProducesResponseType(typeof(SystemSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken = default)
    {
        var settings = await _systemSettingsService.GetSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// 取得外觀識別設定（登入頁未驗證即可讀取）
    /// </summary>
    [HttpGet("branding")]
    [AllowAnonymous]
    [Function("GetBranding", "外觀識別", Icon = "fa-solid fa-image", Order = 2, Description = "取得系統 Logo 與標題")]
    [EndpointSummary("取得外觀識別")]
    [EndpointDescription("取得系統 Logo 與標題，供登入頁與版面配置顯示。")]
    [ProducesResponseType(typeof(BrandingSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBranding(CancellationToken cancellationToken = default)
    {
        var branding = await _systemSettingsService.GetBrandingAsync(cancellationToken);

        // 未驗證端點不揭露維護者資訊
        return Ok(new BrandingSettingsDto
        {
            SystemTitle = branding.SystemTitle,
            LogoDataUrl = branding.LogoDataUrl,
            UpdatedAt = branding.UpdatedAt
        });
    }

    /// <summary>
    /// 更新外觀識別設定
    /// </summary>
    [HttpPut("branding")]
    [Function("UpdateBranding", "更新外觀識別", Icon = "fa-solid fa-pen", Order = 3, Description = "更新系統 Logo 與標題")]
    [RequiresPermission("SYSTEM.SYSTEMSETTINGS.UPDATE", "更新系統配置")]
    [EndpointSummary("更新外觀識別")]
    [EndpointDescription("更新系統 Logo（data URL，256 KB 以內）與標題，並即時通知所有線上使用者。")]
    [ProducesResponseType(typeof(BrandingSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> UpdateBranding(
        [FromBody] BrandingSettingsDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var updated = await _systemSettingsService.UpdateBrandingAsync(request, ClientIp, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "更新外觀識別之請求內容無效。");
            return InvalidRequest(ex.Message);
        }
    }

    /// <summary>
    /// 更新 Identity 密碼與帳號鎖定規則
    /// </summary>
    [HttpPut("identity-policy")]
    [Function("UpdateIdentityPolicy", "更新密碼與鎖定規則", Icon = "fa-solid fa-shield-halved", Order = 4, Description = "更新密碼複雜度與登入失敗鎖定規則")]
    [RequiresPermission("SYSTEM.SYSTEMSETTINGS.UPDATE")]
    [EndpointSummary("更新密碼與鎖定規則")]
    [EndpointDescription("更新密碼複雜度與登入失敗鎖定規則，儲存後立即生效（既有密碼不受影響，於下次變更密碼時套用）。")]
    [ProducesResponseType(typeof(IdentityPolicySettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateIdentityPolicy(
        [FromBody] IdentityPolicySettingsDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var updated = await _systemSettingsService.UpdateIdentityPolicyAsync(request, ClientIp, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "更新 Identity 規則之請求內容無效。");
            return InvalidRequest(ex.Message);
        }
    }

    /// <summary>
    /// 取得系統配置異動歷程
    /// </summary>
    [HttpGet("change-logs")]
    [Function("GetSystemSettingChangeLogs", "系統配置異動紀錄", Icon = "fa-solid fa-clock-rotate-left", Order = 5, Description = "檢視系統配置異動歷程")]
    [RequiresPermission("SYSTEM.SYSTEMSETTINGS.READ")]
    [EndpointSummary("系統配置異動紀錄")]
    [EndpointDescription("取得系統配置異動歷程（新到舊，預設 100 筆）。")]
    [ProducesResponseType(typeof(IReadOnlyList<SystemSettingChangeLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChangeLogs([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var logs = await _systemSettingsService.GetChangeLogsAsync(take, cancellationToken);
        return Ok(logs);
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private BadRequestObjectResult InvalidRequest(string detail) => BadRequest(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "無效的請求參數",
        Detail = detail,
        Instance = HttpContext.Request.Path
    });
}
