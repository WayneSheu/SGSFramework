using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Services
{
    /// <summary>
    /// 系統控制器中繼資料服務實作（支援權限綁定樹狀與平坦清單查詢）
    /// </summary>
    public class ControllerMetadataService(
        DbContext dbContext,
        IMemoryCache cache,
        ICurrentUserService currentUserService,
        IPermissionChangeNotifier permissionChangeNotifier,
        TimeProvider timeProvider,
        ILogger<ControllerMetadataService> logger) : IControllerMetadataService
    {
        /// <summary>SignalR 廣播原因代碼，前端據此重新載入選單與系統公告</summary>
        public const string FunctionStatusChangedReason = "function-status-changed";

        private readonly DbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private readonly ICurrentUserService _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        private readonly IPermissionChangeNotifier _permissionChangeNotifier = permissionChangeNotifier ?? throw new ArgumentNullException(nameof(permissionChangeNotifier));
        private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        private readonly ILogger<ControllerMetadataService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<IEnumerable<ControllerMetadataDto>> GetAllControllerMetadatasAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbContext.Set<ControllerMetadata>() //[cite: 10]
                    .AsNoTracking()
                    .Where(m => includeInactive || m.IsActive) //[cite: 10]
                    .OrderBy(m => m.DisplayOrder) //[cite: 10]
                    .Select(m => new ControllerMetadataDto
                    {
                        Id = m.Id, //[cite: 10]
                        Version = m.Version, //[cite: 10]
                        ModuleName = m.ModuleName, //[cite: 10]
                        ModuleTitle = m.ModuleTitle, //[cite: 10]
                        ControllerTitle = m.ControllerTitle, //[cite: 10]
                        ControllerName = m.ControllerName, //[cite: 10]
                        ControllerTypeName = m.ControllerTypeName, //[cite: 10]
                        ActionName = m.ActionName, //[cite: 10]
                        RouteTemplate = m.RouteTemplate, //[cite: 10]
                        Description = m.Description, //[cite: 10]
                        DisplayName = m.DisplayName, //[cite: 10]
                        Icon = m.Icon, //[cite: 10]
                        DisplayOrder = m.DisplayOrder, //[cite: 10]
                        ParentMenuName = m.ParentMenuName, //[cite: 10]
                        PermissionKey = m.PermissionKey, //[cite: 10]
                        IsActive = m.IsActive, //[cite: 10]
                        IsManuallyDisabled = m.IsManuallyDisabled,
                        DisabledNotice = m.DisabledNotice,
                        DisabledAt = m.DisabledAt,
                        ExpectedRestoreAt = m.ExpectedRestoreAt
                    })
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得系統控制器中繼資料清單時發生未預期異常。");
                throw;
            }
        }

        public async Task<ControllerMetadataDto> UpdateFunctionStatusAsync(Guid id, FunctionStatusChange change, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(change);

            if (id == Guid.Empty)
            {
                throw new ArgumentException("識別碼不可為空 Guid。", nameof(id));
            }

            var reason = Normalize(change.Reason);
            var notice = Normalize(change.Notice);
            var now = _timeProvider.GetUtcNow();

            // 停用必須說明原因，作為資安稽核依據（前端亦有檢核，此處為伺服器端強制）
            if (!change.IsActive && reason is null)
            {
                throw new ArgumentException("停用功能時必須填寫停用原因。", nameof(change));
            }

            if (!change.IsActive && change.ExpectedRestoreAt is { } restoreAt && restoreAt <= now)
            {
                throw new ArgumentException("預計恢復時間必須晚於目前時間。", nameof(change));
            }

            try
            {
                var entity = await _dbContext.Set<ControllerMetadata>()
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

                if (entity == null)
                {
                    throw new KeyNotFoundException($"找不到識別碼為 '{id}' 的功能中繼資料。");
                }

                if (!change.IsActive && FunctionStatusRules.IsProtected(entity.ControllerName))
                {
                    throw new InvalidOperationException("「系統功能管理」本身的功能不可停用，以免無法再恢復其他功能的狀態。");
                }

                // 已停用狀態下再次送出停用，視為更新公告內容 / 預計恢復時間，仍需留下稽核紀錄
                bool statusUnchanged = entity.IsActive == change.IsActive && entity.IsManuallyDisabled == !change.IsActive;
                if (statusUnchanged && change.IsActive)
                {
                    _logger.LogInformation("功能識別碼 '{Id}' 已為啟用狀態，無須重複更新。", id);
                    return ToDto(entity);
                }

                entity.IsActive = change.IsActive;
                // 記錄人工決策，避免 API 重啟時的中繼資料同步將停用的功能恢復為啟用
                entity.IsManuallyDisabled = !change.IsActive;

                if (change.IsActive)
                {
                    entity.DisabledNotice = null;
                    entity.DisabledAt = null;
                    entity.ExpectedRestoreAt = null;
                }
                else
                {
                    entity.DisabledNotice = notice ?? $"「{entity.DisplayName}」功能暫停使用，造成不便敬請見諒。";
                    entity.DisabledAt = statusUnchanged ? entity.DisabledAt ?? now : now;
                    entity.ExpectedRestoreAt = change.ExpectedRestoreAt;
                }

                _dbContext.Set<FunctionStatusLog>().Add(new FunctionStatusLog
                {
                    ControllerMetadataId = entity.Id,
                    ControllerName = entity.ControllerName,
                    ActionName = entity.ActionName,
                    DisplayName = entity.DisplayName,
                    PermissionKey = entity.PermissionKey,
                    IsActive = change.IsActive,
                    Reason = reason,
                    Notice = entity.DisabledNotice,
                    ExpectedRestoreAt = entity.ExpectedRestoreAt,
                    ActorUserId = Guid.TryParse(_currentUserService.UserId, out var actorId) ? actorId : null,
                    ActorName = _currentUserService.UserName,
                    ClientIp = change.ClientIp,
                    CreatedAt = now
                });

                await _dbContext.SaveChangesAsync(cancellationToken);

                // 清除 FunctionStatusAuthorizationFilter 的停用清單快取，使變更立即生效
                _cache.Remove(FunctionStatusRules.DisabledFunctionsCacheKey);

                // 即時通知所有已連線使用者重新載入選單與系統公告
                await _permissionChangeNotifier.BroadcastAsync(FunctionStatusChangedReason, cancellationToken);

                _logger.LogWarning("功能狀態變更成功。識別碼: {Id}, 功能: {Function}, 新狀態: {IsActive}, 操作者: {Actor}, 來源 IP: {ClientIp}, 變更原因: {Reason}",
                    id, $"{entity.ControllerName}.{entity.ActionName}", change.IsActive, _currentUserService.UserName ?? "Unknown", change.ClientIp, reason ?? "未提供");

                return ToDto(entity);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("更新功能狀態之操作已被取消。識別碼: {Id}", id);
                throw;
            }
            catch (Exception ex) when (ex is not KeyNotFoundException && ex is not ArgumentException && ex is not InvalidOperationException)
            {
                _logger.LogError(ex, "更新功能狀態時發生資料庫異動異常。識別碼: {Id}", id);
                throw;
            }
        }

        public async Task<IReadOnlyList<FunctionStatusLogDto>> GetFunctionStatusLogsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<FunctionStatusLog>()
                .AsNoTracking()
                .Where(l => l.ControllerMetadataId == id)
                .OrderByDescending(l => l.CreatedAt)
                .Take(200)
                .Select(l => new FunctionStatusLogDto
                {
                    Id = l.Id,
                    IsActive = l.IsActive,
                    Reason = l.Reason,
                    Notice = l.Notice,
                    ExpectedRestoreAt = l.ExpectedRestoreAt,
                    ActorName = l.ActorName,
                    ClientIp = l.ClientIp,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<FunctionNoticeDto>> GetActiveNoticesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<ControllerMetadata>()
                .AsNoTracking()
                .Where(m => m.IsManuallyDisabled)
                .OrderByDescending(m => m.DisabledAt)
                .Select(m => new FunctionNoticeDto
                {
                    Id = m.Id,
                    ModuleTitle = m.ModuleTitle,
                    ControllerTitle = m.ControllerTitle,
                    DisplayName = m.DisplayName,
                    Notice = m.DisabledNotice ?? string.Empty,
                    DisabledAt = m.DisabledAt,
                    ExpectedRestoreAt = m.ExpectedRestoreAt
                })
                .ToListAsync(cancellationToken);
        }

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static ControllerMetadataDto ToDto(ControllerMetadata entity) => new()
        {
            Id = entity.Id,
            ModuleName = entity.ModuleName,
            ModuleTitle = entity.ModuleTitle,
            ControllerTitle = entity.ControllerTitle,
            ControllerName = entity.ControllerName,
            ActionName = entity.ActionName,
            DisplayName = entity.DisplayName,
            PermissionKey = entity.PermissionKey,
            RouteTemplate = entity.RouteTemplate,
            IsActive = entity.IsActive,
            IsManuallyDisabled = entity.IsManuallyDisabled,
            DisabledNotice = entity.DisabledNotice,
            DisabledAt = entity.DisabledAt,
            ExpectedRestoreAt = entity.ExpectedRestoreAt
        };


        public async Task<IEnumerable<ModulePermissionGroupDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default)
        {
            var flatList = await GetAllControllerMetadatasAsync(cancellationToken: cancellationToken);

            // 於記憶體中進行階層群組轉換 (ModuleName -> ControllerName -> Actions)
            var tree = flatList
                .GroupBy(m => new { m.ModuleName, m.ModuleTitle })
                .Select(moduleGroup => new ModulePermissionGroupDto
                {
                    ModuleName = moduleGroup.Key.ModuleName,
                    ModuleTitle = moduleGroup.Key.ModuleTitle,
                    Controllers = moduleGroup
                        .GroupBy(c => new { c.ControllerName, c.ControllerTitle, c.Icon, c.DisplayOrder })
                        .Select(controllerGroup => new ControllerPermissionGroupDto
                        {
                            ControllerName = controllerGroup.Key.ControllerName,
                            ControllerTitle = controllerGroup.Key.ControllerTitle,
                            Icon = controllerGroup.Key.Icon,
                            DisplayOrder = controllerGroup.Key.DisplayOrder,
                            Actions = controllerGroup.OrderBy(a => a.DisplayOrder).ToList()
                        })
                        .OrderBy(c => c.DisplayOrder)
                        .ToList()
                })
                .ToList();

            return tree;
        }



    }
}
