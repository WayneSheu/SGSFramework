using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Filters
{
    public sealed class LaboratoryAuthorizationFilter(
            ILaboratoryAuthorizationService labAuthService,
            ILogger<LaboratoryAuthorizationFilter> logger,
            string[] allowedCategories) : IAsyncAuthorizationFilter
    {
        private readonly ILaboratoryAuthorizationService _labAuthService = labAuthService ?? throw new ArgumentNullException(nameof(labAuthService));
        private readonly ILogger<LaboratoryAuthorizationFilter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly string[] _allowedCategories = allowedCategories ?? Array.Empty<string>();

        private const string RequestLabCacheKey = "SGS_Request_Laboratory_Category_Cache";

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
            {
                return;
            }

            var httpContext = context.HttpContext;

            if (httpContext.User?.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("[LabAuthFilter] 嘗試存取受保護的實驗室資源，但未提供有效身份憑證。Path: {Path}", httpContext.Request.Path);

                context.Result = new UnauthorizedObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "身份驗證失敗",
                    Detail = "存取此端點需要有效的 JWT Bearer Token 憑證。",
                    Instance = httpContext.Request.Path
                });
                return;
            }

            try
            {
                // 檢查請求生命週期快取，避免重複進行實驗室層級查詢
                var isAuthorized = httpContext.Items[RequestLabCacheKey] as bool?;

                if (!isAuthorized.HasValue)
                {
                    isAuthorized = await _labAuthService.ValidateUserLaboratoryCategoryAsync(
                        httpContext.User,
                        _allowedCategories,
                        httpContext.RequestAborted);

                    httpContext.Items[RequestLabCacheKey] = isAuthorized.Value;
                }

                if (!isAuthorized.Value)
                {
                    _logger.LogWarning("[LabAuthFilter] 使用者 {User} 的當前實驗室分類不符合允許範圍 [{Categories}]。Path: {Path}",
                        httpContext.User.Identity?.Name ?? "Unknown", string.Join(", ", _allowedCategories), httpContext.Request.Path);

                    context.Result = new ObjectResult(new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "實驗室區域權限不足",
                        Detail = $"您的當前實驗室類別不在允許存取的範圍內 ({string.Join(", ", _allowedCategories)})。",
                        Instance = httpContext.Request.Path
                    })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LabAuthFilter] 驗證實驗室類別時發生內部錯誤。Path: {Path}", httpContext.Request.Path);

                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "伺服器內部錯誤",
                    Detail = "驗證實驗室區域權限時發生未預期的系統異常。",
                    Instance = httpContext.Request.Path
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }
}
