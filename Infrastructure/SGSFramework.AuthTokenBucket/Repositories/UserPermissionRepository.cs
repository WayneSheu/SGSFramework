using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Identities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Repositories
{
    /// <summary>
    /// 使用者權限與位元遮罩資料存取實作 (Entity Framework Core)
    /// </summary>
    public class UserPermissionRepository(
    DbContext context,
    ILogger<UserPermissionRepository> logger) : IUserPermissionRepository
    {
        private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly ILogger<UserPermissionRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<Dictionary<string, long>> GetPermissionsByLabAsync(
            string userId,
            Guid labId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);

            try
            {
                var records = await _context.Set<UserLabPermission>()
                    .Where(x => x.UserId == userId && x.TenantLabId == labId)
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
                _logger.LogError(ex, "從資料庫查詢使用者實驗室權限發生異常。UserId: {UserId}, LabId: {LabId}", userId, labId);
                return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public async Task<Dictionary<string, long>> GetGlobalPermissionsAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);

            try
            {
                var records = await _context.Set<UserGlobalPermission>()
                    .Where(x => x.UserId == userId)
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

        public async Task<bool> SaveUserLabPermissionsAsync(
            string userId,
            Guid labId,
            Dictionary<string, long> permissions,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);
            permissions ??= new(StringComparer.OrdinalIgnoreCase);

            try
            {
                var dbSet = _context.Set<UserLabPermission>();

                var existingRecords = await dbSet
                    .Where(x => x.UserId == userId && x.TenantLabId == labId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var existingDict = existingRecords.ToDictionary(x => x.ControllerOrModuleKey, StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in permissions)
                {
                    if (existingDict.TryGetValue(kvp.Key, out var existingEntity))
                    {
                        if (existingEntity.Bitmask != kvp.Value)
                        {
                            existingEntity.Bitmask = kvp.Value;
                            existingEntity.UpdatedAt = DateTime.UtcNow;
                            dbSet.Update(existingEntity);
                        }
                        existingDict.Remove(kvp.Key);
                    }
                    else
                    {
                        var newEntity = new UserLabPermission
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            TenantLabId = labId,
                            ControllerOrModuleKey = kvp.Key,
                            Bitmask = kvp.Value,
                            CreatedAt = DateTime.UtcNow
                        };
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
                _logger.LogError(ex, "儲存使用者實驗室權限至資料庫時發生異常。UserId: {UserId}, LabId: {LabId}", userId, labId);
                return false;
            }
        }

        /// <summary>
        /// 實作使用者的全域/組織級權限寫入與 Upsert / Delete Diff 邏輯
        /// </summary>
        public async Task<bool> SaveUserGlobalPermissionsAsync(
            string userId,
            Dictionary<string, long> permissions,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);
            permissions ??= new(StringComparer.OrdinalIgnoreCase);

            try
            {
                var dbSet = _context.Set<UserGlobalPermission>();

                var existingRecords = await dbSet
                    .Where(x => x.UserId == userId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var existingDict = existingRecords.ToDictionary(x => x.PermissionKey, StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in permissions)
                {
                    if (existingDict.TryGetValue(kvp.Key, out var existingEntity))
                    {
                        if (existingEntity.Bitmask != kvp.Value)
                        {
                            existingEntity.Bitmask = kvp.Value;
                            existingEntity.UpdatedAt = DateTime.UtcNow;
                            dbSet.Update(existingEntity);
                        }
                        existingDict.Remove(kvp.Key);
                    }
                    else
                    {
                        var newEntity = new UserGlobalPermission
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            PermissionKey = kvp.Key,
                            Bitmask = kvp.Value,
                            CreatedAt = DateTime.UtcNow
                        };
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
