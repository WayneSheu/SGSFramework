namespace PhysLIMS.API.Extensions
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Hosting;
    using System;
    using System.IO;

    public static class SpaFallbackExtensions
    {
        /// <summary>
        /// 註冊安全的 Vue/React SPA 兜底路由，自動排除 API 與系統文件請求
        /// </summary>
        public static IEndpointConventionBuilder MapProtectedSpaFallback(
            this IEndpointRouteBuilder endpoints,
            IWebHostEnvironment environment,
            string fallbackFile = "index.html")
        {
            if (endpoints == null) throw new ArgumentNullException(nameof(endpoints));
            if (environment == null) throw new ArgumentNullException(nameof(environment));

            string webRoot = environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string indexPath = Path.Combine(webRoot, fallbackFile);

            // 若 wwwroot/index.html 實體檔案不存在，則不啟用 Fallback，避免干擾 API 路由
            if (!File.Exists(indexPath))
            {
                return new DummyEndpointConventionBuilder();
            }

            // 使用正則表達式排除 api, scalar, openapi 路徑
            return endpoints.MapFallbackToFile("{*path:regex(^(?!(api|scalar|openapi)).*$)}", fallbackFile);
        }

        private sealed class DummyEndpointConventionBuilder : IEndpointConventionBuilder
        {
            public void Add(Action<EndpointBuilder> convention) { }
        }
    }
}