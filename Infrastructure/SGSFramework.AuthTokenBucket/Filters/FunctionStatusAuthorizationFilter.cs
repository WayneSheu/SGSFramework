using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Controller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Filters
{
    /// <summary>
    /// 全域功能狀態過濾器：拒絕呼叫已於「系統功能管理」手動停用的 API (Controller Action)。
    /// 停用清單快取於 IMemoryCache，變更功能狀態時由 ControllerMetadataService 主動清除，故變更可立即生效。
    /// </summary>
    public sealed class FunctionStatusAuthorizationFilter(
        ITokenDbContext dbContext,
        IMemoryCache cache,
        ILogger<FunctionStatusAuthorizationFilter> logger) : IAsyncAuthorizationFilter
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        private readonly ITokenDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private readonly ILogger<FunctionStatusAuthorizationFilter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            {
                return;
            }

            var controllerName = descriptor.ControllerTypeInfo.Name;
            if (FunctionStatusRules.IsProtected(controllerName))
            {
                return;
            }

            var httpContext = context.HttpContext;

            try
            {
                var disabled = await _cache.GetOrCreateAsync(FunctionStatusRules.DisabledFunctionsCacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CacheDuration;

                    var rows = await _dbContext.Set<ControllerMetadata>()
                        .AsNoTracking()
                        .Where(m => m.IsManuallyDisabled)
                        .Select(m => new { m.ControllerName, m.ActionName, m.DisabledNotice, m.ExpectedRestoreAt })
                        .ToListAsync(httpContext.RequestAborted);

                    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var r in rows)
                    {
                        map.TryAdd(FunctionStatusRules.BuildKey(r.ControllerName, r.ActionName), BuildDetail(r.DisabledNotice, r.ExpectedRestoreAt));
                    }
                    return map;
                });

                var key = FunctionStatusRules.BuildKey(controllerName, descriptor.MethodInfo.Name);
                if (disabled is null || !disabled.TryGetValue(key, out var detail))
                {
                    return;
                }

                _logger.LogWarning("[FunctionStatusFilter] 拒絕呼叫已停用之功能 [{Function}]。User: {User}, Path: {Path}",
                    key, httpContext.User?.Identity?.Name ?? "Anonymous", httpContext.Request.Path);

                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "功能已停用",
                    Detail = detail,
                    Instance = httpContext.Request.Path
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 狀態檢查失敗時不阻斷請求，仍由後續權限過濾器把關，避免資料庫異常導致全站 API 失效
                _logger.LogError(ex, "[FunctionStatusFilter] 檢查功能停用狀態時發生錯誤，略過檢查。Path: {Path}", httpContext.Request.Path);
            }
        }


        /// <summary>
        /// 組成回傳給使用者的停用說明：公告內容 + 預計恢復時間 (以台北時間顯示)
        /// </summary>
        private static string BuildDetail(string? notice, DateTimeOffset? expectedRestoreAt)
        {
            var text = string.IsNullOrWhiteSpace(notice) ? "此功能已由系統管理員停用，暫時無法使用。" : notice.Trim();
            if (expectedRestoreAt is { } restoreAt)
            {
                text += $"（預計恢復時間：{restoreAt.ToOffset(TimeSpan.FromHours(8)):yyyy/MM/dd HH:mm}）";
            }
            return text;
        }
    }
}
