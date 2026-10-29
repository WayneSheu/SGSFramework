using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Serilog.Context;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.HttpAuditProviders;
using System.Reflection;

namespace SGSFramework.SystemLog.Middlewares
{
    public class RouteContextMiddleware
    {
        private const string UnknownModule = "Unknown";
        private const string StaticAssetsModule = "StaticAssets";

        private readonly RequestDelegate _next;

        public RouteContextMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task InvokeAsync(HttpContext context, IAuditProvider auditProvider)
        {
            var (moduleName, operation, operationDesc, controllerName) = Resolve(context);

            // 1. 取得使用者資訊 (支援 Fallback 預設值)
            var userId = auditProvider?.UserId;
            var userName = auditProvider?.UserName ?? (string.IsNullOrEmpty(userId) ? "System" : userId);

            // 2. 🚀 從 Request Header 或 Query 取出表頭區域實驗室 (Lab Id / TenantId)
            string? tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                               ?? context.Request.Headers["X-Lab-Id"].FirstOrDefault()
                               ?? context.Items["TenantId"]?.ToString();

            // 3. 取得 IP 與 URL
            var ip = context.Connection.RemoteIpAddress?.ToString();
            var url = context.Request.Path;

            context.Items["ModuleName"] = moduleName;
            context.Items["Operation"] = operation;
            context.Items["OperationDesc"] = operationDesc;
            context.Items["ControllerName"] = controllerName;
            context.Items["UserId"] = userId;
            context.Items["UserName"] = userName;
            context.Items["TenantId"] = tenantId;
            context.Items["IP"] = ip;
            context.Items["Url"] = url;

            // 4. 推入 Serilog LogContext 確保非同步日誌能完整攜帶
            using (LogContext.PushProperty("ModuleName", moduleName))
            using (LogContext.PushProperty("Operation", operation))
            using (LogContext.PushProperty("OperationDesc", operationDesc))
            using (LogContext.PushProperty("ControllerName", controllerName))
            using (LogContext.PushProperty("UserId", userId ?? (object)DBNull.Value))
            using (LogContext.PushProperty("UserName", userName))
            using (LogContext.PushProperty("TenantId", tenantId ?? (object)DBNull.Value))
            using (LogContext.PushProperty("IP", ip ?? string.Empty))
            using (LogContext.PushProperty("Url", url.ToString()))
            {
                await _next(context);
            }
        }

        private static (string ModuleName, string Operation, string OperationDesc, string ControllerName) Resolve(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint is null)
            {
                return (StaticAssetsModule, "AssetRequest", "靜態資源請求", "Unknown");
            }

            var descriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (descriptor is null)
            {
                var moduleFromMetadata = endpoint.Metadata.GetMetadata<ModuleAttribute>()?.ModuleName ?? UnknownModule;
                return (moduleFromMetadata, endpoint.DisplayName ?? "Unknown", "非MVC端點請求", "Unknown");
            }

            var controllerType = descriptor.ControllerTypeInfo;
            var controllerName = descriptor.ControllerName;

            var moduleAttr = descriptor.MethodInfo.GetCustomAttribute<ModuleAttribute>()
                             ?? controllerType.GetCustomAttribute<ModuleAttribute>()
                             ?? controllerType.Assembly.GetCustomAttribute<ModuleAttribute>();

            var controllerTitleAttr = controllerType.GetCustomAttribute<ControllerTitleAttribute>();
            string controllerTitleName = controllerTitleAttr?.Title ?? controllerName;

            var functionAttr = descriptor.MethodInfo.GetCustomAttribute<FunctionAttribute>();
            string functionTitleName = functionAttr?.Title ?? functionAttr?.FunctionName ?? descriptor.ActionName;

            // 💡 組合操作說明：包含 ControllerName 與中文標題
            string operationDesc = $"{controllerTitleName} ({controllerName}) > {functionTitleName}";

            var operation = !string.IsNullOrWhiteSpace(functionAttr?.FunctionName)
                ? functionAttr!.FunctionName
                : $"{controllerName}.{descriptor.ActionName}";

            return (moduleAttr?.ModuleName ?? UnknownModule, operation, operationDesc, controllerName);
        }
    }

    public static class RouteContextMiddlewareExtensions
    {
        public static IApplicationBuilder UseRouteContextAnalysis(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RouteContextMiddleware>();
        }

        /// <summary>
        /// 將 CorrelationId Middleware 加入 Pipeline（讀取 / 產生 X-Correlation-ID，並寫入日誌上下文）。
        /// </summary>
        public static IApplicationBuilder UseCorrelationIdContext(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CorrelationIdMiddleware>();
        }
    }
}