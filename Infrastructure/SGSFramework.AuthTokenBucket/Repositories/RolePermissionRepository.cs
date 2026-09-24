using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Repositories
{
    /// <summary>
    /// 角色權限與 64 位元遮罩資料存取倉儲實作
    /// </summary>
    public sealed class RolePermissionRepository : IRolePermissionRepository
    {
        private readonly DbContext _dbContext;
        private readonly ILogger<RolePermissionRepository> _logger;

        public RolePermissionRepository(
            DbContext dbContext,
            ILogger<RolePermissionRepository> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, long>> GetPermissionsByLabAsync(
            string roleId,
            Guid labId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(roleId);

            try
            {
                // 注意：若系統有 RoleLabPermission 實體可在此查詢，此處示範標準權限 Key-Bitmask 映射讀取
                return await _dbContext.Set<RoleGlobalPermission>()
                    .AsNoTracking()
                    .Where(x => x.RoleId == roleId)
                    .ToDictionaryAsync(
                        k => k.PermissionKey,
                        v => v.Bitmask,
                        StringComparer.OrdinalIgnoreCase,
                        cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢角色實驗室權限時發生例外。RoleId: {RoleId}, LabId: {LabId}", roleId, labId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, long>> GetGlobalPermissionsAsync(
            string roleId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(roleId);

            try
            {
                return await _dbContext.Set<RoleGlobalPermission>()
                    .AsNoTracking()
                    .Where(x => x.RoleId == roleId)
                    .ToDictionaryAsync(
                        k => k.PermissionKey,
                        v => v.Bitmask,
                        StringComparer.OrdinalIgnoreCase,
                        cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢角色全域權限時發生例外。RoleId: {RoleId}", roleId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SaveRoleLabPermissionsAsync(
            string roleId,
            Guid labId,
            Dictionary<string, long> permissions,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(roleId);
            ArgumentNullException.ThrowIfNull(permissions);

            // 實驗室級別權限更新重用通用批次邏輯
            return await SaveRoleGlobalPermissionsAsync(roleId, permissions, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> SaveRoleGlobalPermissionsAsync(
            string roleId,
            Dictionary<string, long> permissions,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(roleId);
            ArgumentNullException.ThrowIfNull(permissions);

            var dbSet = _dbContext.Set<RoleGlobalPermission>();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. 取得資料庫中該角色現有的權限紀錄
                var existingEntities = await dbSet
                    .Where(x => x.RoleId == roleId)
                    .ToDictionaryAsync(x => x.PermissionKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

                var nowUtc = DateTime.UtcNow;
                var nowOffset = DateTimeOffset.UtcNow;

                // 2. 處理刪除與更新/新增 (Upsert 邏輯)
                // A. 移除不在新權限字典中的舊紀錄
                var keysToRemove = existingEntities.Keys
                    .Except(permissions.Keys, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    dbSet.Remove(existingEntities[key]);
                }

                // B. 新增或更新權限
                foreach (var (permKey, bitmask) in permissions)
                {
                    if (string.IsNullOrWhiteSpace(permKey))
                    {
                        continue;
                    }

                    string cleanKey = permKey.Trim();

                    if (existingEntities.TryGetValue(cleanKey, out var existingEntity))
                    {
                        // 若數值有變更才進行更新
                        if (existingEntity.Bitmask != bitmask)
                        {
                            existingEntity.Bitmask = bitmask;
                            existingEntity.UpdatedAt = nowUtc;
                            existingEntity.UpdatedAtUtc = nowOffset;
                            dbSet.Update(existingEntity);
                        }
                    }
                    else
                    {
                        // 新增實體
                        var newEntity = new RoleGlobalPermission
                        {
                            Id = Guid.NewGuid(),
                            RoleId = roleId,
                            PermissionKey = cleanKey,
                            Bitmask = bitmask,
                            CreatedAt = nowUtc,
                            CreatedAtUtc = nowOffset
                        };
                        await dbSet.AddAsync(newEntity, cancellationToken);
                    }
                }

                // 3. 提交持久化變更
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("成功更新角色全域權限。RoleId: {RoleId}, 總權限模組數: {Count}", roleId, permissions.Count);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "更新角色全域權限交易失敗並已復原。RoleId: {RoleId}", roleId);
                throw;
            }
        }
    }
}
