// ==========================================
// 檔案路徑: src/Presentation/SGSFramework.Identity/Controllers/v1/UserManagementController.cs
// 架構層級: Presentation / API Controller Layer (Thin Controller with ResetPassword delegated to Service)
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Controllers.v1;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.Paginations;
using SGSFramework.Core.Results;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.Abstractions.Strategies;
using SGSFramework.Identity.DTOs;
using SGSFramework.Identity.DTOs.Strategies;
using SGSFramework.Identity.DTOs.Users;
using SGSFramework.Identity.Options;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 企業級使用者管理 API 控制器 (遵循 Clean Architecture 與 Result/Error 規範)
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/users")]
[ControllerTitle("使用者管理", Icon = "fa-solid fa-user-gear", Order = 10, Description = "提供使用者分頁查詢、帳號建立、資料更新、密碼重設與生命週期管理服務")]
[RequiresPermission("SYSTEM.USERMANAGEMENT.READ")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public sealed class UserManagementController(
    IUserManagementService userService,
    IUserProvisioningStrategyFactory strategyFactory,
    IOptions<UserProvisioningOptions> provisioningOptions,
    ILogger<UserManagementController> logger) : ApiControllerBase
{
    private readonly IUserManagementService _userService = userService ?? throw new ArgumentNullException(nameof(userService));
    private readonly IUserProvisioningStrategyFactory _strategyFactory = strategyFactory ?? throw new ArgumentNullException(nameof(strategyFactory));
    private readonly UserProvisioningOptions _provisioningOptions = provisioningOptions?.Value ?? throw new ArgumentNullException(nameof(provisioningOptions));
    private readonly ILogger<UserManagementController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// 取得系統所有使用者清單（含所屬角色，自動過濾已軟刪除項目）
    /// </summary>
    [HttpGet]
    [Function("GetUsers", "查詢使用者列表", Icon = "fa-solid fa-users", Order = 9, Description = "取得系統所有有效使用者清單，包含帳號、Email、驗證狀態與所屬角色等資訊", IsMenu = true)]
    [EndpointSummary("查詢使用者列表")]
    [EndpointDescription("取得系統所有有效使用者清單，包含帳號、Email、驗證狀態與所屬角色等資訊")]
    [ProducesResponseType(typeof(Result<List<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.READ")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _userService.GetUsersAsync(cancellationToken).ConfigureAwait(false);
            return HandleResult(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementController] 查詢使用者列表作業已被用戶端取消 (Client Closed Request)。");
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserManagementController] 查詢使用者列表時發生系統異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢使用者列表時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 分頁查詢使用者列表
    /// </summary>
    [HttpGet("paged")]
    [Function("GetPagedUsers", "分頁查詢使用者", Icon = "fa-solid fa-users", Order = 1, Description = "分頁取得系統使用者清單，支援關鍵字搜尋與條件過濾", IsMenu = true)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.READ")]
    [EndpointSummary("分頁查詢使用者")]
    [EndpointDescription("分頁取得系統使用者清單，支援關鍵字搜尋與條件過濾。")]
    [ProducesResponseType(typeof(Result<PagedResult<UserResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPagedUsers(
        [FromQuery] UserQueryParameters queryParameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryParameters);

        try
        {
            var result = await _userService.GetPagedUsersAsync(queryParameters, cancellationToken).ConfigureAwait(false);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分頁查詢使用者列表時發生未預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢使用者分頁清單時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 依據 ID 取得特定使用者詳細資料
    /// </summary>
    [HttpGet("{id:guid}")]
    [Function("GetUserById", "取得使用者詳情", Icon = "fa-solid fa-user", Order = 2, Description = "依據唯一識別碼讀取特定使用者的詳細屬性")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.READ")]
    [EndpointSummary("取得使用者詳情")]
    [EndpointDescription("依據唯一識別碼讀取特定使用者的詳細屬性。")]
    [ProducesResponseType(typeof(Result<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "必須提供有效的使用者識別碼。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var result = await _userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得使用者詳情時發生異常。UserId: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "讀取使用者詳細資料時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    [HttpPost("register")]
    [Function("Register", "建立使用者", Icon = "fa-solid fa-user-gear", Order = 3, Description = "透過指定的策略模式建立新使用者帳號(支援自訂帳號與 Email 雙重唯一性校驗)並指派實驗室與角色")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.CREATE")]
    [EndpointSummary("建立使用者")]
    [EndpointDescription("透過指定的策略模式建立新使用者帳號(支援自訂帳號與 Email 雙重唯一性校驗)並指派實驗室與角色。")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register(
         [FromBody] UserProvisioningRequest request,
         CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            if (!Enum.TryParse<UserProvisioningStrategyType>(_provisioningOptions.StrategyType, true, out var strategyType))
            {
                _logger.LogWarning("伺服器端設定的策略類型無效或無法識別: {StrategyType}", _provisioningOptions.StrategyType);
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "無效的策略類型設定",
                    Detail = $"系統設定的策略類型 '{_provisioningOptions.StrategyType}' 不支援。",
                    Instance = HttpContext.Request.Path
                });
            }

            var strategy = _strategyFactory.GetStrategy(strategyType);

            int? defaultLabId = 0;
            var context = new UserProvisioningContext(
                request.UserName,
                request.Email,
                request.Password,
                defaultLabId,
                request.TenantLabId,
                request.DefaultRole ?? string.Empty
            );

            var result = await strategy.ProvisionUserAsync(context, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                return CreatedAtAction(nameof(GetUserById), new { id = result.Value }, result);
            }

            return HandleResult(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "找不到對應的使用者配置策略實作: {StrategyType}", _provisioningOptions.StrategyType);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的策略類型",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行使用者配置策略時發生未預期異常。Username: {Username}", request.UserName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "執行使用者配置策略時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 確認使用者電子郵件
    /// </summary>
    [HttpPost("confirm-email")]
    [Function("ConfirmEmail", "確認電子郵件", Icon = "fa-solid fa-envelope-circle-check", Order = 11, Description = "透過驗證權杖確認使用者的電子郵件地址")]
    [AllowAnonymous]
    [EndpointSummary("確認電子郵件")]
    [EndpointDescription("透過驗證權杖確認使用者的電子郵件地址。")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "無效的請求參數",
                    Detail = "必須提供有效的使用者識別碼。",
                    Instance = HttpContext.Request.Path
                });
            }

            var result = await _userService.ConfirmEmailAsync(userId, request.Token, cancellationToken).ConfigureAwait(false);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "確認電子郵件時發生未預期異常。UserId: {UserId}", request.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "確認電子郵件時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 更新使用者基本資料與角色配置
    /// </summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateUser", "更新使用者", Icon = "fa-solid fa-user-pen", Order = 4, Description = "更新指定使用者的基本屬性及角色授權配置")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.UPDATE")]
    [EndpointSummary("更新使用者")]
    [EndpointDescription("更新指定使用者的基本屬性及角色授權配置。")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateUser(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (id == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "必須提供有效的使用者識別碼。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var result = await _userService.UpdateUserAsync(id, request, cancellationToken).ConfigureAwait(false);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新使用者資料時發生異常。UserId: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "更新使用者作業時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 管理者強制重設使用者密碼
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [Function("ResetPassword", "重設密碼", Icon = "fa-solid fa-key", Order = 5, Description = "管理員主動重設指定使用者的密碼並重置憑證")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.RESETPASSWORD")]
    [EndpointSummary("重設密碼")]
    [EndpointDescription("管理員主動重設指定使用者的密碼並重置憑證。")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPassword(
        [FromRoute] Guid id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _userService.ResetPasswordAsync(id, request, clientIp, cancellationToken).ConfigureAwait(false);
            return HandleResult(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementController] 重設使用者密碼作業已被用戶端取消。UserId: {UserId}", id);
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重設使用者密碼端點發生未預期異常。UserId: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "重設密碼作業期間發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 切換使用者啟用/停用狀態
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Function("ToggleUserStatus", "切換使用者狀態", Icon = "fa-solid fa-user-lock", Order = 6, Description = "啟用或停用指定使用者的系統存取權限")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.UPDATE")]
    [EndpointSummary("切換使用者狀態")]
    [EndpointDescription("啟用或停用指定使用者的系統存取權限。")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ToggleUserStatus(
        [FromRoute] Guid id,
        [FromQuery] bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "必須提供有效的使用者識別碼。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var result = await _userService.ToggleUserStatusAsync(id, isActive, cancellationToken).ConfigureAwait(false);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切換使用者狀態時發生異常。UserId: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "切換使用者狀態作業時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 刪除指定使用者
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Function("DeleteUser", "刪除使用者", Icon = "fa-solid fa-user-slash", Order = 7, Description = "刪除指定使用者帳號並清理相關關聯與權限")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.DELETE")]
    [EndpointSummary("刪除使用者")]
    [EndpointDescription("刪除指定使用者帳號並清理相關關聯與權限。")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteUser(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "必須提供有效的使用者識別碼。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var result = await _userService.DeleteUserAsync(id, cancellationToken).ConfigureAwait(false);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除使用者時發生異常。UserId: {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "刪除使用者作業時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 取得指定使用者的完整角色指派狀態 (包含未繫結角色)
    /// </summary>
    [HttpGet("{userId:guid}/roles")]
    [Function("GetUserRoleAssignment", "查詢使用者角色設定", Icon = "fa-solid fa-user-tag", Order = 10, Description = "取得特定使用者包含已指派與未指派的全系統角色")]
    [EndpointSummary("查詢使用者角色設定")]
    [EndpointDescription("取得特定使用者包含已指派與未指派的全系統角色。")]
    [ProducesResponseType(typeof(Result<UserRoleAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.READ")]
    public async Task<IActionResult> GetUserRoleAssignment(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "必須提供有效的使用者識別碼。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _userService.GetUserRoleAssignmentAsync(userId, cancellationToken).ConfigureAwait(false);
            return HandleResult(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementController] 查詢使用者角色狀態作業已被用戶端取消。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserManagementController] 查詢使用者角色狀態時發生異常。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "讀取使用者角色設定時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 忘記密碼 - 申請重設權限 (產生加密重設記號)
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [Function("ForgotPassword", "忘記密碼", Icon = "fa-solid fa-unlock-keyhole", Order = 5, Description = "發送密碼重設郵件與記號至使用者信箱")]
    [EndpointSummary("忘記密碼")]
    [EndpointDescription("發送密碼重設郵件與記號至使用者信箱。")]
    [ProducesResponseType(typeof(Result<ForgotPasswordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            var result = await _userService.ForgotPasswordAsync(request, clientIp, cancellationToken).ConfigureAwait(false);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "忘記密碼端點發生未預期異常。Email: {Email}", request.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "發送重設密碼請求時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}