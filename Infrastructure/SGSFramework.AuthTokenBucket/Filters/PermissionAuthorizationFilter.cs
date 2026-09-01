using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Filters
{
    /// <summary>
    /// 全域/區域權限授權過濾器，讀取 Endpoint 上的 RequiresPermissionAttribute 元資料並進行授權比對。
    /// 透過建構子注入與請求生命週期快取優化效能，嚴格遵守 RESTful 規範。
    /// </summary>
    public sealed class PermissionAuthorizationFilter(
        IPermissionAuthorizationService authService,
        ILogger<PermissionAuthorizationFilter> logger) : IAsyncAuthorizationFilter
    {
        private readonly IPermissionAuthorizationService _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        private readonly ILogger<PermissionAuthorizationFilter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private const string RequestPermissionsCacheKey = "SGS_Request_Permission_Evaluation_Cache";

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
            {
                return;
            }

            var permissionAttributes = context.ActionDescriptor.EndpointMetadata
                .OfType<RequiresPermissionAttribute>()
                .ToList();

            if (permissionAttributes.Count == 0)
            {
                return;
            }

            var httpContext = context.HttpContext;

            try
            {
                if (httpContext.User?.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("[AuthFilter] 請求嘗試存取受保護資源，但未提供有效身份憑證。Path: {Path}", httpContext.Request.Path);

                    context.Result = new UnauthorizedObjectResult(new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "身份驗證失敗",
                        Detail = "存取此端點需要有效的 JWT Bearer Token 憑證。",
                        Instance = httpContext.Request.Path
                    });
                    return;
                }

                // 建立或取得請求生命週期內的權限驗證快取字典，避免同一請求重複查詢
                var evaluatedPermissions = httpContext.Items[RequestPermissionsCacheKey] as Dictionary<string, bool>
                    ?? [];

                foreach (var attr in permissionAttributes)
                {
                    if (!evaluatedPermissions.TryGetValue(attr.PermissionKey, out bool hasPermission))
                    {
                        hasPermission = await _authService.HasPermissionAsync(httpContext.User, attr.PermissionKey, httpContext.RequestAborted);
                        evaluatedPermissions[attr.PermissionKey] = hasPermission;
                        httpContext.Items[RequestPermissionsCacheKey] = evaluatedPermissions;
                    }

                    if (!hasPermission)
                    {
                        _logger.LogWarning("[AuthFilter] 使用者 {User} 欠缺必備權限點 [{PermissionKey}]。Path: {Path}",
                            httpContext.User.Identity?.Name ?? "Unknown", attr.PermissionKey, httpContext.Request.Path);

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
                _logger.LogError(ex, "[AuthFilter] 執行授權過濾器時發生內部錯誤。Path: {Path}", httpContext.Request.Path);
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