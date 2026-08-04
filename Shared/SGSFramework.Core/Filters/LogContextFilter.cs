using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog.Context;
using SGSFramework.Core.Abstractions.Attributes;
using System.Reflection;

namespace SGSFramework.Core.Filters
{
    /// <summary>
    /// 實作一個非同步的 action 過濾器，將模組、操作、IP 位址與 URL 等資訊加入記錄上下文中，以用於每個 HTTP 請求。
    /// </summary>
    /// <remarks>此過濾器會從控制器與動作（action）的屬性擷取元資料，並在請求期間將其注入記錄上下文。此作法可強化日誌的關聯性與可追蹤性。    
    /// 此過濾器適用於需要結構化日誌與上下文資訊的 ASP.NET Core 應用程式。注入的屬性在請求範圍內對下游的記錄器可見。</remarks>
    public class LogContextFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)

        {
            // 1. 取得 Controller 上的 ModuleAttribute
            var moduleAttr = context.Controller.GetType().GetCustomAttribute<ModuleAttribute>();

            // 2. 取得 Method 上的 FunctionAttribute
            var functionAttr = context.ActionDescriptor is ControllerActionDescriptor descriptor
                ? descriptor.MethodInfo.GetCustomAttribute<FunctionAttribute>()
                : null;

            // 3. 準備注入的屬性
            var moduleName = moduleAttr?.ModuleName ?? "UnknownModule";
            var operationName = functionAttr?.FunctionName ?? context.ActionDescriptor.DisplayName;

            // 4. 使用 LogContext 注入，這會涵蓋整個 Request Scope
            using (LogContext.PushProperty("ModuleName", moduleName))
            using (LogContext.PushProperty("Operation", operationName))
            using (LogContext.PushProperty("IP", context.HttpContext.Connection.RemoteIpAddress?.ToString()))
            using (LogContext.PushProperty("Url", context.HttpContext.Request.Path))
            {
                await next();
            }
        }
    }
}


