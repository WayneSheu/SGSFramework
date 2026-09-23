using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.AuditLogs;
using SGSFramework.Core.Abstractions.DbContexts;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuditLog.Services.Permissions
{
    public class UserPermissionAuditService<TDbContext> : IUserPermissionAuditService
      where TDbContext : DbContext, ITokenDbContext
    {
        private readonly TDbContext _dbContext;
        private readonly ILogger<UserPermissionAuditService<TDbContext>> _logger;

        public UserPermissionAuditService(
            TDbContext dbContext,
            ILogger<UserPermissionAuditService<TDbContext>> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task AuditPermissionChangeAsync(
            string operatorId,
            string targetUserId,
            string permissionKey,
            long oldBitmask,
            long newBitmask,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetUserId);
            ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

            try
            {
                // 1. 計算位元差異
                long changed = oldBitmask ^ newBitmask;
                long addedMask = changed & newBitmask;
                long removedMask = changed & oldBitmask;

                if (changed == 0)
                {
                    _logger.LogInformation("[Audit] 權限 Bitmask 未發生變更，跳過稽核紀錄。 TargetUser: {UserId}, PermissionKey: {Key}", targetUserId, permissionKey);
                    return;
                }

                // 2. 構建詳細日誌與可檢索 JSON 資料
                var auditDetails = new
                {
                    PermissionKey = permissionKey,
                    OldBitmask = oldBitmask,
                    NewBitmask = newBitmask,
                    AddedBitmask = addedMask,
                    RemovedBitmask = removedMask,
                    Changes = new
                    {
                        AddedPositions = GetBitPositions(addedMask),
                        RemovedPositions = GetBitPositions(removedMask)
                    }
                };

                // 3. 寫入 AuditLog 實體 (範例寫入通用 AuditLogs 表)
                var auditLog = new
                {
                    Id = Guid.NewGuid().ToString(),
                    OperatorId = operatorId,
                    TargetUserId = targetUserId,
                    ActionType = "UPDATE_USER_PERMISSION",
                    EntityName = "User_Global_Permissions",
                    DetailsJson = System.Text.Json.JsonSerializer.Serialize(auditDetails),
                    CreatedAt = DateTime.UtcNow
                };

                // 實務上請替換為專案對應的 Entity 與 DbSet
                // _dbContext.Set<AuditLog>().Add(auditLog);
                // await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("[Audit Success] 使用者 {OperatorId} 修改了 {TargetUserId} 在 {PermissionKey} 的權限。 AddedMask: {Added}, RemovedMask: {Removed}",
                    operatorId, targetUserId, permissionKey, addedMask, removedMask);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Audit Error] 寫入權限稽核日誌時發生例外。 OperatorId: {OperatorId}, TargetUserId: {TargetUserId}", operatorId, targetUserId);
                throw;
            }
        }

        /// <summary>
        /// 將 Bitmask 轉為包含的 Bit 位元位置清單 (例如 5 => Bit Position 0, 2)
        /// </summary>
        private static List<int> GetBitPositions(long mask)
        {
            var positions = new List<int>();
            for (int i = 0; i < 64; i++)
            {
                if ((mask & (1L << i)) != 0)
                {
                    positions.Add(i);
                }
            }
            return positions;
        }
    }
}
