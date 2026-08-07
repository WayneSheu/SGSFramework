using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SGSFramework.AuthTokenBucket.Controllers.Base;
using SGSFramework.AuthTokenBucket.Servers;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using System;

namespace SGSFramework.Identity.Controllers
{
    /// <summary>
    /// 企業級微軟 Entra ID 整合單一登入端點
    /// </summary>
    [ApiController]
    [Route("api/auth/sso")]
    [Menu("微軟登入端點", "fa-solid fa-user-shield", order: 2, parent: null)]
    // [ApiExplorerSettings(GroupName = "Auth")] // 讓 Scalar 順利將其歸類到 Auth 紅框群組中
    public sealed class MicrosoftSsoController : AdSsoController<ApplicationUser>
    {
        /// <summary>
        /// 建構子：透過 base(...) 將 DI 注入之依賴元件精準傳遞給父類別建構子
        /// </summary>
        /// <param name="tokenEngine">令牌桶快取與 Token 核發引擎</param>
        /// <param name="userManager">Identity 使用者管理服務</param>
        /// <param name="configuration">系統組態設定</param>
        public MicrosoftSsoController(
            TokenBucketEngine<ApplicationUser> tokenEngine,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration)
            : base(
                  tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine)),
                  userManager ?? throw new ArgumentNullException(nameof(userManager)),
                  configuration ?? throw new ArgumentNullException(nameof(configuration)))
        {
        }
    }
}