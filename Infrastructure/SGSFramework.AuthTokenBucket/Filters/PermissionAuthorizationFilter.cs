using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Identities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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

            // 系統帳號（M2M）與系統專用端點：以 Scope 授權，與人類使用者的權限點檢查互斥
            if (EvaluateSystemScope(context) is { } systemScopeResult)
            {
                if (systemScopeResult is not AllowedResult)
                {
                    context.Result = systemScopeResult;
                }
                return;
            }

            // 僅需登入的自身資料端點（[Authorize] 仍會確保已驗證）
            if (context.ActionDescriptor.EndpointMetadata.OfType<AuthenticatedOnlyAttribute>().Any())
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

        /// <summary>
        /// 系統帳號 Scope 授權：
        /// 1. 系統帳號權杖：僅能存取標註 [RequiresSystemScope] 的端點（預設拒絕），且需具備全部指定 Scope，略過人類權限點檢查。
        /// 2. 人類使用者：存取未開放 AllowUsers 的系統專用端點時拒絕。
        /// 回傳 null 表示不屬於系統帳號情境，交由一般權限點流程判斷。
        /// </summary>
        private IActionResult? EvaluateSystemScope(AuthorizationFilterContext context)
        {
            var httpContext = context.HttpContext;
            var scopeAttributes = context.ActionDescriptor.EndpointMetadata
                .OfType<RequiresSystemScopeAttribute>()
                .ToList();

            if (httpContext.User.IsSystemAccount())
            {
                var accountId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

                if (scopeAttributes.Count == 0)
                {
                    _logger.LogWarning("[AuthFilter] 系統帳號 {AccountId} 嘗試存取未開放給系統帳號的端點。Path: {Path}", accountId, httpContext.Request.Path);
                    return Forbidden(httpContext, "系統帳號僅能存取開放給系統帳號（標註 RequiresSystemScope）的端點。");
                }

                var grantedScopes = httpContext.User.GetScopes();
                var missingScopes = scopeAttributes
                    .Where(a => a.Scope is not null && !grantedScopes.Contains(a.Scope))
                    .Select(a => a.Scope!)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (missingScopes.Count > 0)
                {
                    _logger.LogWarning("[AuthFilter] 系統帳號 {AccountId} 欠缺操作範圍 [{Scopes}]。Path: {Path}",
                        accountId, string.Join(", ", missingScopes), httpContext.Request.Path);
                    return Forbidden(httpContext, $"系統帳號尚未被授予此操作所需的操作範圍 ({string.Join(", ", missingScopes)})。");
                }

                return AllowedResult.Instance;
            }

            if (scopeAttributes.Count > 0 &&
                !scopeAttributes.Any(a => a.AllowUsers) &&
                httpContext.User?.Identity?.IsAuthenticated == true)
            {
                _logger.LogWarning("[AuthFilter] 使用者 {User} 嘗試存取系統帳號專用端點。Path: {Path}",
                    httpContext.User.Identity?.Name ?? "Unknown", httpContext.Request.Path);
                return Forbidden(httpContext, "此端點僅供系統帳號（服務間通訊）呼叫。");
            }

            return null;
        }

        private static ObjectResult Forbidden(HttpContext httpContext, string detail) => new(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "存取權限不足",
            Detail = detail,
            Instance = httpContext.Request.Path
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };

        /// <summary>系統帳號已通過 Scope 檢查的標記結果（不寫入 context.Result）</summary>
        private sealed class AllowedResult : IActionResult
        {
            public static readonly AllowedResult Instance = new();

            public Task ExecuteResultAsync(ActionContext context) => Task.CompletedTask;
        }
    }
}