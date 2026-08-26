using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ApiInfrastructure.Filters
{
    /// <summary>
    /// 全域/區域權限授權過濾器，讀取 Endpoint 上的 RequiresPermissionAttribute 元資料並進行授權比對。
    /// 嚴格遵守 RESTful 規範：未認證回應 401 Unauthorized，權限不足回應 403 Forbidden。
    /// </summary>
    public sealed class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // 1. 若端點允許匿名存取 ([AllowAnonymous])，直接放行
            if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
            {
                return;
            }

            // 2. 擷取 Controller 與 Action 上的所有 RequiresPermissionAttribute 宣告
            var permissionAttributes = context.ActionDescriptor.EndpointMetadata
                .OfType<RequiresPermissionAttribute>()
                .ToList();

            if (permissionAttributes.Count == 0)
            {
                return;
            }

            var httpContext = context.HttpContext;
            var logger = httpContext.RequestServices.GetService<ILogger<PermissionAuthorizationFilter>>();

            try
            {
                // 3. 步驟一：身份驗證 (Authentication) 檢查
                if (httpContext.User?.Identity?.IsAuthenticated != true)
                {
                    logger?.LogWarning("[AuthFilter] 請求嘗試存取受保護資源，但未提供有效身份憑證。Path: {Path}", httpContext.Request.Path);

                    context.Result = new UnauthorizedObjectResult(new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "身份驗證失敗",
                        Detail = "存取此端點需要有效的 JWT Bearer Token 憑證。",
                        Instance = httpContext.Request.Path
                    });
                    return;
                }

                // 4. 步驟二：權限授權 (Authorization) 檢查
                var authService = httpContext.RequestServices.GetRequiredService<IPermissionAuthorizationService>();

                foreach (var attr in permissionAttributes)
                {
                    bool hasPermission = await authService.HasPermissionAsync(httpContext.User, attr.PermissionKey, httpContext.RequestAborted);
                    if (!hasPermission)
                    {
                        logger?.LogWarning("[AuthFilter] 使用者 {User} 欠缺必備權限點 [{PermissionKey}]。Path: {Path}",
                            httpContext.User.Identity?.Name ?? "Unknown", attr.PermissionKey, httpContext.Request.Path);

                        // 精確回傳 403 Forbidden
                        context.Result = new ObjectResult(new ProblemDetails
                        {
                            Status = StatusCodes.Status403Forbidden,
                            Title = "存取權限不足",
                            Detail = $"您的帳號尚未被授予執行此操作所需的權限點 ({attr.PermissionKey})。",
                            Instance = httpContext.Request.Path
                        })
                        {
                            StatusCode = StatusCodes.Status403Forbidden
                        };
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[AuthFilter] 執行授權過濾器時發生內部錯誤。Path: {Path}", httpContext.Request.Path);
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "伺服器內部錯誤",
                    Detail = "驗證使用者權限時發生未預期的系統異常。",
                    Instance = httpContext.Request.Path
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }
}
