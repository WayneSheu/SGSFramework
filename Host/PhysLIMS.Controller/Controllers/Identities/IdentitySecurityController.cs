using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.AuthTokenBucket.Servers;
using SGSFramework.Core.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;

namespace SGSFramework.ApiInfrastructure.Controllers.Identities
{
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Auth")]
    [Menu("身分安全管理", "fa-solid fa-user-shield", order: 2, parent: "系統管理")]
    public sealed class IdentitySecurityController : ControllerBase
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
        /// 帳號遭竊緊急熔斷端點（後端主動防禦方案）
        /// </summary>
        [HttpPost("emergency-freeze")]
        [Menu("帳號緊急熔斷", "fa-solid fa-lock", order: 1, parent: "身分安全管理")]
        [RequiresPermission("IDENTITY_SECURITY_EMERGENCY_FREEZE")]
        [Description("帳號遭竊緊急熔斷端點")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EmergencyFreeze([FromBody] AccountRiskRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return BadRequest(new { message = "必須提供風險帳號識別碼 (UserId)。" });
            }

            try
            {
                string reason = request.Reason ?? "系統自動偵測資安異常或使用者主動通報憑證洩漏。";

                bool isFrozen = await _tokenEngine.EmergencyFreezeAsync(request.UserId, reason);

                if (!isFrozen)
                {
                    _logger.LogWarning("[Security-Event:SEC-FREEZE-FAIL] 執行緊急熔斷失敗，找不到對應的使用者帳號。UserId: {UserId}", request.UserId);
                    return BadRequest(new { message = "執行緊急熔斷失敗，找不到對應的使用者帳號。" });
                }

                _logger.LogWarning("[Security-Event:SEC-FREEZE-SUCCESS] 帳號 {UserId} 已成功啟動緊急熔斷。原因: {Reason}", request.UserId, reason);

                return Ok(new
                {
                    Success = true,
                    Message = "已成功啟動資安緊急熔斷防禦！該用戶所有裝置的工作階段已被強制銷毀，並進入鎖定狀態。",
                    FrozenAtUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行緊急熔斷時發生系統核心異常。UserId: {UserId}", request.UserId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = $"執行緊急熔斷時發生系統核心異常：{ex.Message}" });
            }
        }

        /// <summary>
        /// 身分補償與重設密碼完成端點（解凍帳號並清理風險環境）
        /// </summary>
        [HttpPost("remediate-account")]
        [Menu("身分重設與解凍", "fa-solid fa-unlock", order: 2, parent: "身分安全管理")]
        [RequiresPermission("IDENTITY_SECURITY_REMEDIATE")]
        [Description("身分補償與重設密碼完成端點")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemediateAccount([FromBody] AccountRemediationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return BadRequest(new { message = "必須提供解凍之使用者識別碼 (UserId)。" });
            }

            try
            {
                bool isCleared = await _tokenEngine.CompleteRemediationAsync(request.UserId);

                _logger.LogInformation("使用者身分 {UserId} 已完成重設與解凍清理。", request.UserId);

                return Ok(new
                {
                    Success = true,
                    Message = "使用者身分已重驗完成，風險 Session 已全數安全清理，該帳號已回復正常登入權限。",
                    RemediatedAtUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行身分補償環境解凍時發生系統核心異常。UserId: {UserId}", request.UserId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = $"執行身分補償環境解凍時發生系統核心異常：{ex.Message}" });
            }
        }


    }

    /// <summary>
    /// Account Risk Request Model
    /// </summary>
    public sealed class AccountRiskRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Account Remediation Request Model
    /// </summary>
    public sealed class AccountRemediationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string? VerificationMethod { get; set; }
    }
}