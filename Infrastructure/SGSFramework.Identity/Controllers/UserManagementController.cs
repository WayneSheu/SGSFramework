using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.AuthTokenBucket.Servers;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using System;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SGSFramework.Identity.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Menu("使用者管理", "fa-solid fa-user-shield", order: 10, parent: null)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT")]
    public sealed class UserManagementController : ApiControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly TokenBucketEngine<IdentityUser> _tokenEngine;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            TokenBucketEngine<IdentityUser> tokenEngine,
            ILogger<UserManagementController> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 使用者註冊 (支援自訂帳號與 Email 雙重唯一性校驗)
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Menu("使用者註冊", "fa-solid fa-user-shield", order: 1, parent: "使用者管理")]
        [RequiresPermission("SYSTEM.USERMANAGEMENT.REGISTER")]
        [Description("註冊使用者")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var userByName = await _userManager.FindByNameAsync(request.Username);
                if (userByName != null)
                {
                    return BadRequest(new { message = "該帳號名稱已被使用。" });
                }

                var userByEmail = await _userManager.FindByEmailAsync(request.Email);
                if (userByEmail != null)
                {
                    return BadRequest(new { message = "該電子郵件已被註冊。" });
                }

                var user = new IdentityUser
                {
                    UserName = request.Username,
                    Email = request.Email,
                    EmailConfirmed = false,
                    LockoutEnabled = true
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "註冊失敗", errors = result.Errors.Select(e => e.Description) });
                }

                string emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                _logger.LogInformation("用戶 [{Username}] 註冊成功，已生成驗證信憑證。", user.UserName);

                return Ok(new { message = "註冊成功，請至電子郵件信箱查收驗證信。", debugToken = emailToken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "註冊端點發生未預期異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "伺服器內部錯誤" });
            }
        }

        /// <summary>
        /// 2. 驗證電子郵件
        /// </summary>
        [HttpGet("confirm-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Menu("使用者Email驗證", "fa-solid fa-user-shield", order: 2, parent: "使用者管理")]
        [RequiresPermission("SYSTEM.USERMANAGEMENT.CONFIRMEMAIL")]
        [Description("驗證使用者Email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string email, [FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { message = "無效的驗證參數。" });
            }

            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    return BadRequest(new { message = "找不到該使用者。" });
                }

                var result = await _userManager.ConfirmEmailAsync(user, token);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "電子郵件驗證失敗或記號已過期。" });
                }

                return Ok(new { message = "電子郵件驗證成功！帳號已正式啟用。" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "驗證電子郵件端點發生未預期異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "伺服器內部錯誤" });
            }
        }

        /// <summary>
        /// 登入端點 (支援自訂帳號或 Email 雙軌識別 + 2FA 分流)
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status423Locked)]
        [Menu("一般帳號登入", "fa-solid fa-user-shield", order: 3, parent: "使用者管理")]
        [Description("一般帳號登入")]
        public async Task<IActionResult> Login([FromBody] ManagementLoginRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var user = await _userManager.FindByNameAsync(request.AccountIdentifier)
                           ?? await _userManager.FindByEmailAsync(request.AccountIdentifier);

                if (user == null)
                {
                    return Unauthorized(new { message = "帳號、Email 或密碼錯誤。" });
                }

                if (await _userManager.IsLockedOutAsync(user))
                {
                    return StatusCode(StatusCodes.Status423Locked, new { message = "帳號因連續登入失敗已被系統鎖定，請稍後再試。" });
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("用戶 {Account} 觸發連續失敗鎖定。", user.UserName);
                    return StatusCode(StatusCodes.Status423Locked, new { message = "密碼錯誤次數過多，帳號已被鎖入黑洞。" });
                }

                if (result.RequiresTwoFactor)
                {
                    string code = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
                    return Ok(new { requiresTwoFactor = true, message = "已啟用雙因子驗證，請輸入郵件驗證碼。", debug2FaCode = code });
                }

                if (!result.Succeeded)
                {
                    return Unauthorized(new { message = "帳號、Email 或密碼錯誤。" });
                }

                string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
                string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Generic Browser";
                string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                var tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);

                return Ok(new { message = "登入成功", tokenData = tokenResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登入端點發生未預期異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "伺服器內部錯誤" });
            }
        }

        /// <summary>
        /// 4. 驗證雙因子登入 (2FA)
        /// </summary>
        [HttpPost("verify-2fa")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Menu("雙因子驗證登入", "fa-solid fa-user-shield", order: 4, parent: "使用者管理")]
        [RequiresPermission("SYSTEM.USERMANAGEMENT.VERIFYTWOFACTOR")]
        [Description("驗證雙因子登入")]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] TwoFactorVerificationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email) ?? await _userManager.FindByNameAsync(request.Email);
                if (user == null) return Unauthorized(new { message = "認證失敗。" });

                var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, "Email", request.Code);
                if (!isValid)
                {
                    await _userManager.AccessFailedAsync(user);
                    return Unauthorized(new { message = "雙因子驗證碼錯誤。" });
                }

                await _userManager.ResetAccessFailedCountAsync(user);

                string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
                string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Generic Browser";
                string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                var tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);

                return Ok(new { message = "雙因子認證成功", tokenData = tokenResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "2FA 驗證端點發生未預期異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "伺服器內部錯誤" });
            }
        }

        /// <summary>
        /// 5. 忘記密碼 - 申請重設權限 (產生加密重設記號)
        /// </summary>
        [HttpPost("forgot-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Menu("忘記密碼", "fa-solid fa-user-shield", order: 5, parent: "使用者管理")]
        [RequiresPermission("SYSTEM.USERMANAGEMENT.FORGOTPASSWORD")]
        [Description("忘記密碼")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    return Ok(new { message = "若帳號存在且已完成啟用，重設密碼信件已發送至您的信箱。" });
                }

                string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                _logger.LogInformation("用戶 {Email} 申請密碼重設記號成功。", request.Email);

                return Ok(new { message = "重設密碼信件已發送。", debugResetToken = resetToken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "忘記密碼端點發生未預期異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "伺服器內部錯誤" });
            }
        }

        /// <summary>
        /// 6. 重設密碼執行 (忘記密碼情境，使用 Token 換新密碼)
        /// </summary>
        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Menu("重設密碼", "fa-solid fa-user-shield", order: 6, parent: "使用者管理")]
        [RequiresPermission("SYSTEM.USERMANAGEMENT.RESETPASSWORD")]
        [Description("重設密碼")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return BadRequest(new { message = "密碼重設失敗。" });
                }

                var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "密碼重設失敗，可能記號已失效或密碼強度不符。", errors = result.Errors.Select(e => e.Description) });
                }

                // 🚀 雙軌資安聯防：強制登出該用戶全網所有其餘工作階段
                await _tokenEngine.EmergencyFreezeAsync(user.Id, "使用者透過忘記密碼功能完成密碼重設，全面肅清舊有憑證軌跡。");
                await _tokenEngine.CompleteRemediationAsync(user.Id);

                return Ok(new { message = "密碼重設成功，已強制終止其餘裝置連線，請使用新密碼重新登入。" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "密碼重設端點發生未預期異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "伺服器內部錯誤" });
            }
        }

        /// <summary>
        /// 🚀 7. 變更密碼 (登入狀態情境：驗證舊密碼並換新密碼，強制執行登出聯防)
        /// </summary>
        [Authorize]
        [HttpPost("change-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Menu("變更密碼", "fa-solid fa-user-shield", order: 7, parent: "使用者管理")]
        [RequiresPermission("SYSTEM.USERMANAGEMENT.CHANGEPASSWORD")]
        [Description("變更密碼")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 從登入憑證的 Claims 中提取目前使用者 ID
                string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "無法識別目前的登入身分。" });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return BadRequest(new { message = "使用者不存在。" });
                }

                // 核心操作：驗證舊密碼並寫入新密碼
                var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "變更密碼失敗，請確認舊密碼是否輸入正確或符合密碼複雜度原則。", errors = result.Errors.Select(e => e.Description) });
                }

                // 🚀 核心資安聯防：變更密碼成功後，立即使外流的所有舊 JWT 與長效 Refresh Token 立即死掉，防禦 Session 劫持
                await _tokenEngine.EmergencyFreezeAsync(user.Id, "使用者執行線上變更密碼，強制登出全網所有裝置工作階段。");
                await _tokenEngine.CompleteRemediationAsync(user.Id);

                _logger.LogInformation("用戶 [{Username}] 已成功在線上變更密碼並肅清全網 Session。", user.UserName);

                return Ok(new { message = "密碼變更成功，其餘裝置連線已被安全強制切斷，請使用新密碼重新登入。" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "變更密碼端點發生未預期核心異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "伺服器內部錯誤", detail = ex.Message });
            }
        }

        /// <summary>
        /// 8. 安全登出 (註銷當前裝置之活動軌跡)
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Menu("安全登出", "fa-solid fa-user-shield", order: 8, parent: "使用者管理")]
        [RequiresPermission("SYSTEM.USERMANAGEMENT.LOGOUT")]
        [Description("安全登出")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";

                if (!string.IsNullOrEmpty(userId))
                {
                    await _tokenEngine.EmergencyFreezeAsync(userId, $"用戶主動執行安全登出。裝置識別: {deviceId}");
                    await _tokenEngine.CompleteRemediationAsync(userId);
                    _logger.LogInformation("用戶 {UserId} 自裝置 {DeviceId} 安全登出。", userId, deviceId);
                }

                return Ok(new { message = "已安全登出並註銷工作階段票據。" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "安全登出端點發生未預期異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "伺服器內部錯誤" });
            }
        }
    }

    #region DTO 模型定義載體

    public sealed class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class ManagementLoginRequest
    {
        public string AccountIdentifier { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class TwoFactorVerificationRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public sealed class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public sealed class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public sealed class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    #endregion
}