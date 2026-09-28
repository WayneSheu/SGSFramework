// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuthTokenBucket/Repositories/UserPermissionRepository.cs
// 架構層級: Infrastructure Layer (Repository Implementation)
// ==========================================

#nullable enable

namespace SGSFramework.AuthTokenBucket.Repositories
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using SGSFramework.Core.Abstractions.Entities.Identities;
    using SGSFramework.Core.Abstractions.Permissions.Identities;
    using SGSFramework.Core.Abstractions.Permissions.Repositories;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// 使用者權限與位元遮罩資料存取實作 (Entity Framework Core) - 對齊領域實體封裝與工廠方法
    /// </summary>
    public class UserPermissionRepository(
    DbContext context,
    ILogger<UserPermissionRepository> logger) : IUserPermissionRepository
    {
        private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly ILogger<UserPermissionRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <inheritdoc />
        public async Task<Dictionary<string, long>> GetPermissionsByLabAsync(
            string userId,
            Guid tenantLabId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);

            if (!Guid.TryParse(userId, out var userGuid))
            {
                _logger.LogWarning("無效的 UserId 格式: {UserId}", userId);
                return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var records = await _context.Set<UserLabPermission>()
                    .AsNoTracking()
                    .Where(x => x.UserId == userGuid && x.TenantLabId == tenantLabId)
                    .Select(x => new { x.ControllerOrModuleKey, x.Bitmask })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return records.ToDictionary(
                    x => x.ControllerOrModuleKey,
                    x => x.Bitmask,
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "從資料庫查詢使用者實驗室權限發生異常。UserId: {UserId}, TenantLabId: {TenantLabId}", userId, tenantLabId);
                return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, long>> GetGlobalPermissionsAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);

            if (!Guid.TryParse(userId, out var userGuid))
            {
                _logger.LogWarning("無效的 UserId 格式: {UserId}", userId);
                return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var records = await _context.Set<UserGlobalPermission>()
                    .AsNoTracking()
                    .Where(x => x.UserId == userGuid)
                    .Select(x => new { x.PermissionKey, x.Bitmask })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return records.ToDictionary(
                    x => x.PermissionKey,
                    x => x.Bitmask,
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "從資料庫查詢使用者全域權限發生異常。UserId: {UserId}", userId);
                return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <inheritdoc />
        public async Task<bool> SaveUserLabPermissionsAsync(
            string userId,
            Guid tenantLabId,
            Dictionary<string, long> permissions,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);
            permissions ??= new(StringComparer.OrdinalIgnoreCase);

            if (!Guid.TryParse(userId, out var userGuid))
            {
                _logger.LogError("儲存實驗室權限失敗，無效的 UserId 格式: {UserId}", userId);
                return false;
            }

            try
            {
                var dbSet = _context.Set<UserLabPermission>();

                var labMapping = await _context.Set<UserLabMapping>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == userGuid && x.TenantLabId == tenantLabId, cancellationToken)
                    .ConfigureAwait(false);

                if (labMapping == null)
                {
                    _logger.LogWarning("找不到使用者 [{UserId}] 與租戶實驗室 [{TenantLabId}] 的有效對應，無法儲存權限。", userId, tenantLabId);
                    return false;
                }

                var existingRecords = await dbSet
                    .Where(x => x.UserId == userGuid && x.TenantLabId == tenantLabId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var existingDict = existingRecords.ToDictionary(x => x.ControllerOrModuleKey, StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in permissions)
                {
                    var normalizedKey = kvp.Key.Trim().ToUpperInvariant();
                    if (existingDict.TryGetValue(normalizedKey, out var existingEntity))
                    {
                        if (existingEntity.Bitmask != kvp.Value)
                        {
                            existingEntity.UpdateBitmask(kvp.Value, userId);
                            dbSet.Update(existingEntity);
                        }
                        existingDict.Remove(normalizedKey);
                    }
                    else
                    {
                        var newEntity = UserLabPermission.Create(
                            userGuid,
                            labMapping.LabId,
                            tenantLabId,
                            normalizedKey,
                            kvp.Value,
                            userId);

                        dbSet.Add(newEntity);
                    }
                }

                if (existingDict.Count > 0)
                {
                    dbSet.RemoveRange(existingDict.Values);
                }

                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "儲存使用者實驗室權限至資料庫時發生異常。UserId: {UserId}, TenantLabId: {TenantLabId}", userId, tenantLabId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SaveUserGlobalPermissionsAsync(
            string userId,
            Dictionary<string, long> permissions,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);
            permissions ??= new(StringComparer.OrdinalIgnoreCase);

            if (!Guid.TryParse(userId, out var userGuid))
            {
                _logger.LogError("儲存全域權限失敗，無效的 UserId 格式: {UserId}", userId);
                return false;
            }

            try
            {
                var dbSet = _context.Set<UserGlobalPermission>();

                var existingRecords = await dbSet
                    .Where(x => x.UserId == userGuid)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var existingDict = existingRecords.ToDictionary(x => x.PermissionKey, StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in permissions)
                {
                    var normalizedKey = kvp.Key.Trim().ToUpperInvariant();
                    if (existingDict.TryGetValue(normalizedKey, out var existingEntity))
                    {
                        if (existingEntity.Bitmask != kvp.Value)
                        {
                            // 修正 CS0272 與封裝原則：改用實體提供的 UpdateBitmask 方法
                            existingEntity.UpdateBitmask(kvp.Value, userId);
                            dbSet.Update(existingEntity);
                        }
                        existingDict.Remove(normalizedKey);
                    }
                    else
                    {
                        // 修正 CS0272 錯誤：改用 UserGlobalPermission.Create 領域工廠方法進行初始化
                        var newEntity = UserGlobalPermission.Create(
                            userGuid,
                            normalizedKey,
                            kvp.Value,
                            userId);

                        dbSet.Add(newEntity);
                    }
                }

                if (existingDict.Count > 0)
                {
                    dbSet.RemoveRange(existingDict.Values);
                }

                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "儲存使用者全域權限至資料庫時發生異常。UserId: {UserId}", userId);
                return false;
            }
        }
    }
}