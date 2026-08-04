using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Servers;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace SGSFramework.ApiInfrastructure.Base
{

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    [ApiController]
    [Route("api/auth/sso/windows")]
    public abstract class WindowsAuthController<TUser> : ApiControllerBase where TUser : IdentityUser, new()
    {
        private readonly TokenBucketEngine<TUser> _tokenEngine;
        private readonly UserManager<TUser> _userManager;

        public WindowsAuthController(TokenBucketEngine<TUser> tokenEngine, UserManager<TUser> userManager)
        {
            _tokenEngine = tokenEngine;
            _userManager = userManager;
        }

        ///// <summary>
        ///// 地端 Windows 網域無感單一登入端點
        ///// </summary>
        //[HttpGet("login")]
        //[Authorize(AuthenticationSchemes = "Windows")] // 🚀 關鍵：強制阻斷，要求 Kerberos/NTLM 握手
        //public async Task<IActionResult> WindowsLogin()
        //{
        //    // 1. 從 HttpContext 中取得經 IIS/Kestrel 驗證過的 Windows 網域身份
        //    var windowsIdentity = HttpContext.User.Identity as WindowsIdentity;
        //    if (windowsIdentity == null || !windowsIdentity.IsAuthenticated)
        //    {
        //        return Unauthorized(new { message = "未通過 Windows 網域認證。" });
        //    }

        //    // 取得網域帳號名稱，例如 "CORP\wayne"
        //    string domainAccount = windowsIdentity.Name;

        //    // 2. 尋找或同步該網域帳號至系統
        //    var user = await _userManager.FindByNameAsync(domainAccount);
        //    if (user == null)
        //    {
        //        user = new TUser { UserName = domainAccount, Email = $"{domainAccount.Split('\\')[1]}@company.com" };
        //        await _userManager.CreateAsync(user);
        //    }

        //    // 3. 綁定當前設備特徵，派發安全水桶 Token
        //    string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "WINDOWS-INTRA-DEVICE";
        //    string initialRawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        //    var tokenResult = await _tokenEngine.RefreshSessionAsync(user.Id, deviceId, initialRawToken);

        //    return Ok(new
        //    {
        //        Message = "Windows 網域單一登入成功",
        //        Identity = domainAccount,
        //        TokenData = tokenResult
        //    });
        //}

        ///// <summary>
        ///// 地端 Windows 網域無感單一登入端點
        ///// </summary>
        //[HttpGet("login")]
        //[Authorize(AuthenticationSchemes = "Windows")] // 🚀 要求 Windows 方案驗證
        //public async Task<IActionResult> WindowsLogin()
        //{
        //    // 1. 調整提取邏輯：優先嘗試轉型真實的 WindowsIdentity，若失敗（開發期模擬）則降級抓取通用 Identity
        //    var userIdentity = HttpContext.User.Identity;

        //    if (userIdentity == null || !userIdentity.IsAuthenticated)
        //    {
        //        return Unauthorized(new { message = "未通過 Windows 網域認證。" });
        //    }

        //    // 取得網域帳號名稱（例如 "CORP\wayne" 或模擬的 "CORP\tony"）
        //    string domainAccount = userIdentity.Name;

        //    if (string.IsNullOrWhiteSpace(domainAccount))
        //    {
        //        return BadRequest(new { message = "無法解析有效的網域帳號名稱。" });
        //    }

        //    // 2. 尋找或同步該網域帳號至系統
        //    var user = await _userManager.FindByNameAsync(domainAccount);
        //    if (user == null)
        //    {
        //        // 擷取名稱部分 (例如 tony) 作為 Email 預設前綴
        //        string accountName = domainAccount.Contains('\\') ? domainAccount.Split('\\')[1] : domainAccount;

        //        user = new TUser
        //        {
        //            UserName = domainAccount,
        //            Email = $"{accountName}@company.com"
        //        };
        //        await _userManager.CreateAsync(user);
        //    }

        //    // 3. 綁定當前設備特徵，派發安全水桶 Token
        //    string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "WINDOWS-INTRA-DEVICE";
        //    string initialRawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        //    var tokenResult = await _tokenEngine.RefreshSessionAsync(user.Id, deviceId, initialRawToken);

        //    return Ok(new
        //    {
        //        Message = "Windows 網域單一登入成功",
        //        Identity = domainAccount,
        //        TokenData = tokenResult
        //    });
        //}



    }
}
