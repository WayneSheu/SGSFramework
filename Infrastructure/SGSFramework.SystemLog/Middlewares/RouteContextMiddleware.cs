using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SGSFramework.Core.Abstractions.Attributes;

namespace SGSFramework.SystemLog.Middlewares
{

    /// <summary>
    /// 在 ASP.NET Core Pipeline 中，分析當前請求的路由 Endpoint，並將 Module 和 Function Name 注入 Context。
    /// </summary>
    public class RouteContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RouteContextMiddleware> _logger;

        public RouteContextMiddleware(RequestDelegate next, ILogger<RouteContextMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        // 注意：此 Middleware 必須在 UseRouting() 和 Endpoint Mapping 前執行 (或與之結合)
        public async Task InvokeAsync(HttpContext context)
        {
            // 1. 獲取當前的 Endpoint Metadata
            var endpoint = context.GetEndpoint();
            string moduleName = "Unknown Module"; // Default value
            string functionName = "Unknown Function";

            if (endpoint != null)
            {
                // 2. 解析 Module Name (使用反射檢查自定義屬性)
                // 我們假設 Module 應從 Endpoint 的 Type (Controller/Class) 或 Method 上尋找
                var moduleAttr = endpoint.Metadata.GetMetadata<ModuleAttribute>();

                if (moduleAttr != null)
                {
                    moduleName = moduleAttr.ModuleName;
                }
                else if (endpoint.Metadata.GetMetadata<ModuleAttribute>() == null)
                // 備用方案：如果沒有自定義屬性，則嘗試從路由路徑的開頭推斷（較不精準）
                {
                    var path = context.Request.Path.ToString();
                    if (path.StartsWith("/api/user")) // 硬編碼或使用更複雜的正規表達式
                    {
                        moduleName = "API_User";
                    }
                }

                // 3. 解析 Function Name
                // 最準確的方式是取得方法名稱，如果沒有明確的做法，就使用路徑片段作為佔位符。
                if (endpoint.Metadata.GetMetadata<ModuleAttribute>() != null)
                {
                    functionName = endpoint.DisplayName ?? "Handler";
                }
                else
                {
                    // fallback: 使用路由模板的最後一部分作為函數名近似值
                    // 2. 使用 'is' 進行型別轉換 (C# 模式比對)
                    if (endpoint is Microsoft.AspNetCore.Routing.RouteEndpoint routeEndpoint)
                    {
                        // 現在可以安全存取 RoutePattern
                        var routeTemplate = routeEndpoint.RoutePattern.RawText;

                        if (!string.IsNullOrEmpty(routeTemplate))
                        {
                            functionName = Path.GetFileName(routeTemplate);
                        }

                        // 範例：輸出路由模板到 Console
                        //Console.WriteLine($"路由模式: {routeEndpoint.RoutePattern}");
                    }


                }

                // 4. 將解析出的 Metadata 儲存到 HttpContext Items，供後續 Middleware 或 Enricher 使用
                context.Items["ModuleName"] = moduleName;
                LogContext.PushProperty("ModuleName", moduleName);
                context.Items["Operation"] = functionName;
                LogContext.PushProperty("Operation", functionName);
            }
            else
            {
                // 如果沒有 Endpoint (例如靜態資源 /images)
                context.Items["ModuleName"] = "StaticAssets";
                LogContext.PushProperty("ModuleName", "StaticAssets");
                context.Items["Operation"] = "AssetRequest";
                LogContext.PushProperty("Operation", "AssetRequest");
            }

            await _next(context);
        }
    }

    public static class RouteContextMiddlewareExtensions
    {
        /// <summary>
        /// 將路由上下文分析 Middleware 加入到 ASP.NET Core Pipeline。
        /// 必須放在 UseRouting() 之後，因為它依賴已解析的 Endpoint Metadata。
        /// </summary>
        public static IApplicationBuilder UseRouteContextAnalysis(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RouteContextMiddleware>();
        }
    }

    // -----------------------------------------------------
    // STEP A: 路由解析 (MUST BE FIRST)
    //app.UseRouting(); 

    // ✨ 【關鍵插入點】在 UseRouting() 後，利用已知的 Endpoint Metadata
    //app.UseRouteContextAnalysis(); // <-- 新增的 Context 解析 Middleware

    // ✨ 【調整與合併】如果您的 Context Logging 和 Route Analysis 是分開的，它們的位置會依賴彼此。
    // 為了簡潔，我們建議將所有 Context 捕獲邏輯（IP, UserID, Module, Function）都放在一個綜合的 Middleware 中執行。

    //app.USGSFrameworktatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

    // ... (後續的其他 Middleware)
}
