using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Serilog.Context;
using SGSFramework.Core.Abstractions.Attributes;
using System.Reflection;

namespace SGSFramework.SystemLog.Middlewares
{
    /// <summary>
    /// 在 UseRouting() 之後解析當前請求的 Endpoint，將「模組名稱 (ModuleName)」與「操作名稱 (Operation)」
    /// 推入 Serilog LogContext，使該請求期間（含驗證、授權、Controller、服務層）的所有日誌都帶有這兩個欄位，
    /// 由 SqlServerLogProcessor 寫入 core.SystemLogs。
    /// </summary>
    /// <remarks>
    /// ModuleName 解析順序：Action 方法 → Controller 類別 → Controller 所在 Assembly 的 [Module]（與選單產生規則一致）。
    /// Operation 解析順序：Action 方法上的 [Function].FunctionName → 「Controller.Action」。
    /// </remarks>
    public class RouteContextMiddleware
    {
        private const string UnknownModule = "Unknown";
        private const string StaticAssetsModule = "StaticAssets";

        private readonly RequestDelegate _next;

        public RouteContextMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var (moduleName, operation) = Resolve(context);

            context.Items["ModuleName"] = moduleName;
            context.Items["Operation"] = operation;

            // 以 using 包住下游管線，請求結束後即還原，避免屬性外洩到其他請求
            using (LogContext.PushProperty("ModuleName", moduleName))
            using (LogContext.PushProperty("Operation", operation))
            {
                await _next(context);
            }
        }

        private static (string ModuleName, string Operation) Resolve(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint is null)
            {
                return (StaticAssetsModule, "AssetRequest");
            }

            var descriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (descriptor is null)
            {
                // 非 MVC Endpoint（SignalR Hub、Minimal API、靜態檔案後備路由等）
                var moduleFromMetadata = endpoint.Metadata.GetMetadata<ModuleAttribute>()?.ModuleName ?? UnknownModule;
                return (moduleFromMetadata, endpoint.DisplayName ?? context.Request.Path.Value ?? "Unknown");
            }

            var controllerType = descriptor.ControllerTypeInfo;

            var moduleAttr = descriptor.MethodInfo.GetCustomAttribute<ModuleAttribute>()
                             ?? controllerType.GetCustomAttribute<ModuleAttribute>()
                             ?? controllerType.Assembly.GetCustomAttribute<ModuleAttribute>();

            var functionAttr = descriptor.MethodInfo.GetCustomAttribute<FunctionAttribute>();
            var controllerName = descriptor.ControllerName;
            var operation = !string.IsNullOrWhiteSpace(functionAttr?.FunctionName)
                ? functionAttr!.FunctionName
                : $"{controllerName}.{descriptor.ActionName}";

            return (moduleAttr?.ModuleName ?? UnknownModule, operation);
        }
    }

    public static class RouteContextMiddlewareExtensions
    {
        /// <summary>
        /// 將路由上下文分析 Middleware 加入 Pipeline。
        /// 必須放在 UseRouting() 之後（需要已解析的 Endpoint），並建議放在 UseAuthentication() 之前，
        /// 讓驗證 / 授權階段的日誌同樣帶有模組與操作欄位。
        /// </summary>
        public static IApplicationBuilder UseRouteContextAnalysis(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RouteContextMiddleware>();
        }

        /// <summary>
        /// 將 CorrelationId Middleware 加入 Pipeline（讀取 / 產生 X-Correlation-ID，並寫入日誌上下文）。
        /// 建議放在管線最前面。
        /// </summary>
        public static IApplicationBuilder UseCorrelationIdContext(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CorrelationIdMiddleware>();
        }
    }
}
