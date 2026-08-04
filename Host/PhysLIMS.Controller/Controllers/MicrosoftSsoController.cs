using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SGSFramework.AuthTokenBucket.Servers;
using SGSFramework.AuthTokenBucket.Controllers.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ApiInfrastructure.Controllers
{
    /// <summary>
    /// 企業級微軟 Entra ID 整合單一登入端點
    /// </summary>
    [ApiController]
    [Route("api/auth/sso")]
   // [ApiExplorerSettings(GroupName = "Auth")] // 讓 Scalar 順利將其歸類到 Auth 紅框群組中
    public sealed class MicrosoftSsoController : AdSsoController<IdentityUser>
    {
        // 透過 : base(...) 將依賴精準傳遞給父類別建構子
        public MicrosoftSsoController(
            TokenBucketEngine<IdentityUser> tokenEngine,
            UserManager<IdentityUser> userManager,
            IConfiguration configuration)
            : base(tokenEngine, userManager, configuration)
        {
        }
    }
}
