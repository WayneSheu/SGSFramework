using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        // 已卸載模組集合版本：變動時（卸載 / 重新上傳）使快取失效，下次請求重建攔截清單
        private static long _seenRemovedModulesVersion;

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
                var removedVersion = FunctionStatusRules.RemovedModulesVersion;
                if (Interlocked.Exchange(ref _seenRemovedModulesVersion, removedVersion) != removedVersion)
                {
                    _cache.Remove(FunctionStatusRules.DisabledFunctionsCacheKey);
                }

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

                    // 已停用模組：模組仍載入於記憶體中 (停用僅更新資料庫狀態)，故於此攔截其所有 API
                    var inactiveModules = await _dbContext.Set<ModuleMetadata>()
                        .AsNoTracking()
                        .Where(m => !m.IsActive)
                        .Select(m => new { m.ModuleName, m.ModuleTitle })
                        .ToListAsync(httpContext.RequestAborted);

                    if (inactiveModules.Count > 0)
                    {
                        var titles = inactiveModules.ToDictionary(m => m.ModuleName, m => m.ModuleTitle, StringComparer.OrdinalIgnoreCase);
                        var moduleNames = titles.Keys.ToList();

                        var moduleRows = await _dbContext.Set<ControllerMetadata>()
                            .AsNoTracking()
                            .Where(c => moduleNames.Contains(c.ModuleName))
                            .Select(c => new { c.ModuleName, c.ControllerName, c.ActionName })
                            .ToListAsync(httpContext.RequestAborted);

                        foreach (var r in moduleRows.Where(r => !FunctionStatusRules.IsProtected(r.ControllerName)))
                        {
                            var title = titles.TryGetValue(r.ModuleName, out var t) && !string.IsNullOrWhiteSpace(t) ? t : r.ModuleName;
                            map.TryAdd(FunctionStatusRules.BuildKey(r.ControllerName, r.ActionName), $"「{title}」模組已由系統管理員停用，暫時無法使用。");
                        }
                    }

                    // 已卸載模組：資料列已移除，但控制器可能仍在記憶體 / 主程式中，同樣攔截其所有 API
                    var removedModules = FunctionStatusRules.GetRemovedModuleNames().ToList();
                    if (removedModules.Count > 0)
                    {
                        var removedRows = await _dbContext.Set<ControllerMetadata>()
                            .AsNoTracking()
                            .Where(c => removedModules.Contains(c.ModuleName))
                            .Select(c => new { c.ModuleName, c.ControllerName, c.ActionName })
                            .ToListAsync(httpContext.RequestAborted);

                        foreach (var r in removedRows.Where(r => !FunctionStatusRules.IsProtected(r.ControllerName)))
                        {
                            map.TryAdd(FunctionStatusRules.BuildKey(r.ControllerName, r.ActionName), $"「{r.ModuleName}」模組已卸載，暫時無法使用。");
                        }
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
