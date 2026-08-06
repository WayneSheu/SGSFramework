using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Servers;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Identity.DTOs;

namespace SGSFramework.Identity.Controllers.v1;

/// <summary>
/// 身份安全管理控制器
/// </summary>
[ApiController]
[Route("api/v1/identity-security")]
[Produces("application/json")]
[Menu("身份安全管理", "fa-solid fa-user-shield", order: 2, parent: null)]
[RequiresPermission("IDENTITY_SECURITY_MANAGEMENT")]
[Description("身份安全管理與緊急風險控制機制")]
public sealed class IdentitySecurityController : ApiControllerBase
{
    private readonly TokenBucketEngine<IdentityUser> _tokenEngine;
    private readonly ILogger<IdentitySecurityController> _logger;

    public IdentitySecurityController(
        TokenBucketEngine<IdentityUser> tokenEngine,
        ILogger<IdentitySecurityController> logger)
    {
        _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 帳號遭竊緊急熔斷（後端主動防禦方案）
    /// </summary>
    /// <param name="request">帳號風險處置請求</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>熔斷結果資訊</returns>
    [HttpPost("emergency-freezes")]
    [Menu("帳號緊急熔斷", "fa-solid fa-lock", order: 1, parent: "身份安全管理")]
    [RequiresPermission("IDENTITY_SECURITY_EMERGENCY_FREEZE")]
    [Description("帳號遭竊緊急熔斷端點，強制銷毀所有工作階段並鎖定")]
    [ProducesResponseType(typeof(EmergencyFreezeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
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
                Detail = $"執行緊急熔斷時發生系統核心異常：{ex.Message}",
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
    [Menu("身分重設與解凍", "fa-solid fa-unlock", order: 2, parent: "身份安全管理")]
    [RequiresPermission("IDENTITY_SECURITY_REMEDIATE")]
    [Description("身分補償與重設密碼完成端點，安全清理風險 Session 並恢復帳號")]
    [ProducesResponseType(typeof(AccountRemediationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
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
                Detail = $"執行身分補償環境解凍時發生系統核心異常：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
    }
}