using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PhysLIMS.API.Models;
using Polly;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace PhysLIMS.API.Helpers
{
    /// <summary>
    /// 開發環境專用：虛擬 Windows 網域驗證處理器
    /// 透過解析 HTTP Header 實現免網域環境下的 SSO 帳號切換模擬
    /// </summary>
    public sealed class FakeWindowsAuthHandler : AuthenticationHandler<FakeWindowsAuthOptions>
    {
        private const string MockUserHeaderKey = "X-Mock-Windows-User";

        public FakeWindowsAuthHandler(
            IOptionsMonitor<FakeWindowsAuthOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        /// <summary>
        /// 核心驗證邏輯
        /// </summary>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                // 1. 嘗試從請求標頭讀取動態模擬的帳號，若無則採用配置的預設值
                string mockAccount = Context.Request.Headers[MockUserHeaderKey].FirstOrDefault() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(mockAccount))
                {
                    mockAccount = $"{Options.DefaultDomain}\\{Options.DefaultUserName}";
                }

                Logger.LogInformation("[Fake-Auth] 正在模擬 Windows 網域驗證，當前帳號: {Account}", mockAccount);

                // 2. 建立符合 Windows 安全識別的 Claims
                var claims = new[]
                {
                new Claim(ClaimTypes.Name, mockAccount),
                new Claim(ClaimTypes.PrimarySid, $"S-1-5-21-MOCK-{Guid.NewGuid().ToString()[..8].ToUpper()}"),
                new Claim(ClaimTypes.Role, Options.DefaultRole)
            };

                //驗證型別必須給予 "Windows"，使 HttpContext.User.Identity.AuthenticationType 為 "Windows"
                var identity = new ClaimsIdentity(claims, "Windows");
                var principal = new ClaimsPrincipal(identity);

                //封裝為驗證票券並回傳成功
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[Fake-Auth] 虛擬驗證處理器執行期間發生核心異常。");
                return Task.FromResult(AuthenticateResult.Fail(ex));
            }
        }
    }
}
