using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Identity.DTOs;

namespace SGSFramework.Identity.Controllers.v1;

/// <summary>
/// 身份安全管理控制器
/// </summary>
[ApiController]
[Route("api/v1/identity-security")]
[Produces("application/json")]
[Authorize]
[ControllerTitle("身份安全管理", Icon = "fa-solid fa-user-shield", Order = 25, Description = "身份安全管理與緊急風險控制機制，支援帳號緊急熔斷與身分補償解凍")]
[RequiresPermission("SYSTEM.IDENTITYSECURITY.RED")]
public sealed class IdentitySecurityController(
    TokenBucketEngine<ApplicationUser> tokenEngine,
    ILogger<IdentitySecurityController> logger) : ApiControllerBase
{
    private readonly TokenBucketEngine<ApplicationUser> _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
    private readonly ILogger<IdentitySecurityController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// 帳號遭竊緊急熔斷（後端主動防禦方案）
    /// </summary>
    /// <param name="request">帳號風險處置請求</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>熔斷結果資訊</returns>
    [HttpPost("emergency-freezes")]
    [Function("EmergencyFreeze", "帳號緊急熔斷", Icon = "fa-solid fa-lock", Order = 1, Description = "帳號遭竊緊急熔斷端點，強制銷毀所有工作階段並鎖定")]
    [ProducesResponseType(typeof(EmergencyFreezeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.IDENTITYSECURITY.EMERGENCYFREEZE")]
    public async Task<IActionResult> EmergencyFreezeAsync(
        [FromBody] AccountRiskRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "請求參數無效",
                Detail = "必須提供風險帳號識別碼 (UserId)。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            string reason = request.Reason ?? "系統自動偵測資安異常或使用者主動通報憑證洩漏。";

            bool isFrozen = await _tokenEngine.EmergencyFreezeAsync(request.UserId, reason);

            if (!isFrozen)
            {
                _logger.LogWarning("[Security-Event:SEC-FREEZE-FAIL] 執行緊急熔斷失敗，找不到對應的使用者帳號。UserId: {UserId}", request.UserId);
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "熔斷執行失敗",
                    Detail = "執行緊急熔斷失敗，找不到對應的使用者帳號。",
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogWarning("[Security-Event:SEC-FREEZE-SUCCESS] 帳號 {UserId} 已成功啟動緊急熔斷。原因: {Reason}", request.UserId, reason);

            return Ok(new EmergencyFreezeResponseDto
            {
                Success = true,
                Message = "已成功啟動資安緊急熔斷防禦！該用戶所有裝置的工作階段已被強制銷毀，並進入鎖定狀態。",
                FrozenAtUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行緊急熔斷時發生系統核心異常。UserId: {UserId}", request.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "系統內部錯誤",
                Detail = "執行緊急熔斷程序時發生系統核心異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 身分補償與重設密碼完成（解凍帳號並清理風險環境）
    /// </summary>
    /// <param name="request">帳號補償與重驗請求</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>身分修復與解凍結果</returns>
    [HttpPost("remediations")]

    [Function("RemediateAccount", "身分重設與解凍", Icon = "fa-solid fa-unlock", Order = 2, Description = "身分補償與重設密碼完成端點，安全清理風險 Session 並恢復帳號")]
    [ProducesResponseType(typeof(AccountRemediationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.IDENTITYSECURITY.REMEDIATE")]
    public async Task<IActionResult> RemediateAccountAsync(
        [FromBody] AccountRemediationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "請求參數無效",
                Detail = "必須提供解凍之使用者識別碼 (UserId)。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            //
            bool isCleared = await _tokenEngine.CompleteRemediationAsync(request.UserId);

            _logger.LogInformation("使用者身分 {UserId} 已完成重設與解凍清理。", request.UserId);

            return Ok(new AccountRemediationResponseDto
            {
                Success = true,
                Message = "使用者身分已重驗完成，風險 Session 已全數安全清理，該帳號已回復正常登入權限。",
                RemediatedAtUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行身分補償環境解凍時發生系統核心異常。UserId: {UserId}", request.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "系統內部錯誤",
                Detail = "執行身分補償環境解凍程序時發生系統核心異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}