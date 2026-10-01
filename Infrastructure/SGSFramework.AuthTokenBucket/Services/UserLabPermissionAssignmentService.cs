// 檔案路徑：Services/UserLabPermissionAssignmentService.cs
#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.UserLabPermissions;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using SGSFramework.Core.Abstractions.Permissions.Identities;
using SGSFramework.Core.Abstractions.Strategies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Services
{
    /// <summary>
    /// 使用者實驗室權限指派與查詢服務 (整合動態權限策略過濾與 ControllerKey 嚴格校驗)
    /// </summary>
    public class UserLabPermissionAssignmentService : IUserLabPermissionAssignmentService
    {
        private readonly ITokenDbContext _dbContext;
        private readonly IEnumerable<ILabPermissionStrategy> _permissionStrategies;
        private readonly ILogger<UserLabPermissionAssignmentService> _logger;

        public UserLabPermissionAssignmentService(
            ITokenDbContext dbContext,
            IEnumerable<ILabPermissionStrategy> permissionStrategies,
            ILogger<UserLabPermissionAssignmentService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _permissionStrategies = permissionStrategies ?? throw new ArgumentNullException(nameof(permissionStrategies));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task AssignOrUpdateLabPermissionAsync(
            Guid userId,
            int labId,
            string controllerOrModuleKey,
            long bitmask,
            string operatorId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(controllerOrModuleKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);

            string normalizedKey = controllerOrModuleKey.Trim().ToUpperInvariant();

            bool isValidControllerKey = await _dbContext.Set<PermissionMetadata>()
                .AnyAsync(p => p.ControllerName.ToUpper() == normalizedKey || p.ModuleName.ToUpper() == normalizedKey, cancellationToken)
                .ConfigureAwait(false);

            if (!isValidControllerKey)
            {
                _logger.LogWarning("指派實驗室權限失敗：無效的 Controller 或模組鍵 [{ControllerKey}]。", normalizedKey);
                throw new ArgumentException($"無效的控制器或模組鍵 [{controllerOrModuleKey}]，該控制器不存在於系統權限元數據中。");
            }

            var labMapping = await _dbContext.Set<UserLabMapping>()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.LabId == labId, cancellationToken)
                .ConfigureAwait(false);

            if (labMapping == null || !labMapping.IsActive)
            {
                throw new InvalidOperationException($"使用者 [{userId}] 與實驗室 [{labId}] 無有效且啟用的對應關係，無法指派專屬權限。");
            }

            if (labMapping.ExpiryDate.HasValue && labMapping.ExpiryDate.Value < DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException($"使用者 [{userId}] 在實驗室 [{labId}] 的對應已逾期，拒絕指派權限。");
            }

            var strategy = _permissionStrategies.FirstOrDefault(s => s.AppliesTo(labMapping.IsPrimary))
                ?? throw new InvalidOperationException($"找不到適用於實驗室型別 (IsPrimary: {labMapping.IsPrimary}) 的權限驗證策略。");

            long filteredBitmask = await strategy.ValidateAndFilterMaskAsync(bitmask, cancellationToken)
                .ConfigureAwait(false);

            if (filteredBitmask != bitmask)
            {
                _logger.LogWarning("使用者 [{UserId}] 針對實驗室 [{LabId}] 申請的權限遮罩部分遭策略攔截。原始: {OriginalMask}, 過濾後: {FilteredMask}",
                    userId, labId, bitmask, filteredBitmask);
            }

            var existingPermission = await _dbContext.Set<UserLabPermission>()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.LabId == labId && x.ControllerOrModuleKey == normalizedKey, cancellationToken)
                .ConfigureAwait(false);

            if (existingPermission != null)
            {
                existingPermission.UpdateBitmask(filteredBitmask, operatorId);
                _logger.LogInformation("更新使用者 [{UserId}] 在實驗室 [{LabId}] 模組 [{Module}] 的權限遮罩。", userId, labId, normalizedKey);
            }
            else
            {
                var newPermission = UserLabPermission.Create(
                    userId,
                    labId,
                    labMapping.TenantLabId,
                    normalizedKey,
                    filteredBitmask,
                    operatorId);

                await _dbContext.Set<UserLabPermission>().AddAsync(newPermission, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("新增使用者 [{UserId}] 在實驗室 [{LabId}] 模組 [{Module}] 的權限遮罩。", userId, labId, normalizedKey);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<List<UserLabPermissionDto>> GetLabPermissionsAsync(
            Guid userId,
            int labId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty || labId <= 0)
            {
                return new List<UserLabPermissionDto>();
            }

            try
            {
                return await _dbContext.Set<UserLabPermission>()
                    .AsNoTracking()
                    .Where(x => x.UserId == userId && x.LabId == labId)
                    .Select(x => new UserLabPermissionDto
                    {
                        ControllerOrModuleKey = x.ControllerOrModuleKey,
                        Bitmask = x.Bitmask
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得使用者 [{UserId}] 在實驗室 [{LabId}] 的專屬權限資料時發生例外。", userId, labId);
                throw;
            }
        }
    }
}