using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;

namespace PhysLIMS.API.Extensions
{
    public static class AuthenticationExtensions
    {
        /// <summary>
        /// authentication 註冊邏輯（防重複註冊）
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddSafeNegotiateAuthentication(this IServiceCollection services)
        {
            // 1. 取得 AuthenticationBuilder 實例
            var authBuilder = services.AddAuthentication(NegotiateDefaults.AuthenticationScheme);

            // 2. 檢查 DI 容器中是否已經註冊過 NegotiateHandler 實作
            // 這樣可防止 Plugin 或多處註冊導致 Scheme 被重複 Add 造成 InvalidOperationException
            var isAlreadyRegistered = services.Any(sd =>
                sd.ImplementationType == typeof(NegotiateHandler) ||
                sd.ServiceType == typeof(NegotiateHandler));

            if (!isAlreadyRegistered)
            {
                // 3. 針對 AuthenticationBuilder 呼叫 AddNegotiate()
                authBuilder.AddNegotiate();
            }

            return services;
        }
    }
}
