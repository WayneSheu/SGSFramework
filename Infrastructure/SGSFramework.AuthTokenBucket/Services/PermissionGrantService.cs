using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionGrants;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions.Identities;
using SGSFramework.Core.Extensions;

namespace SGSFramework.AuthTokenBucket.Services
{
    /// <summary>
    /// 角色實驗室維度 BitMask 權限管理服務實作 (泛型 DbContext 綁定)
    /// </summary>
    public sealed class PermissionGrantService<TContext> : IPermissionGrantService
        where TContext : DbContext
    {
        private readonly TContext _dbContext;
        private readonly ILogger<PermissionGrantService<TContext>> _logger;
        private readonly IPermissionChangeNotifier _permissionChangeNotifier;
        private readonly IRoleAuditService _roleAuditService;

        public PermissionGrantService(
            TContext dbContext, // 要求具體的 TContext 泛型型態
            ILogger<PermissionGrantService<TContext>> logger,
            IPermissionChangeNotifier permissionChangeNotifier,
            IRoleAuditService roleAuditService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _permissionChangeNotifier = permissionChangeNotifier ?? throw new ArgumentNullException(nameof(permissionChangeNotifier));
            _roleAuditService = roleAuditService ?? throw new ArgumentNullException(nameof(roleAuditService));
        }

        public async Task<RoleLabPermissionResponseDto> GetRoleLabPermissionsAsync(
            Guid roleId,
            Guid labId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var grant = await _dbContext.Set<PermissionGrant>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RoleId == roleId && x.LabId == labId, cancellationToken);

                if (grant == null)
                {
                    return new RoleLabPermissionResponseDto
                    {
                        RoleId = roleId,
                        LabId = labId,
                        GrantedBitPositions = [],
                        PermissionVectorBase64 = Convert.ToBase64String(new byte[64])
                    };
                }

                var activePositions = new List<int>();
                byte[] vector = grant.PermissionVector;

                for (int byteIdx = 0; byteIdx < vector.Length; byteIdx++)
                {
                    byte b = vector[byteIdx];
                    if (b == 0) continue;

                    for (int bitIdx = 0; bitIdx < 8; bitIdx++)
                    {
                        if ((b & (1 << bitIdx)) != 0)
                        {
                            activePositions.Add((byteIdx * 8) + bitIdx);
                        }
                    }
                }

                return new RoleLabPermissionResponseDto
                {
                    RoleId = roleId,
                    LabId = labId,
                    GrantedBitPositions = activePositions,
                    PermissionVectorBase64 = Convert.ToBase64String(vector)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢角色實驗室權限時發生未預期異常。RoleId: {RoleId}, LabId: {LabId}", roleId, labId);
                throw;
            }
        }

        public async Task<(bool Succeeded, string Message)> UpdateRoleLabPermissionsAsync(
            Guid roleId,
            Guid labId,
            UpdateRolePermissionsRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 已停用 / 已鎖定的角色不再授予任何權限：禁止異動其實驗室權限設定
                var role = await _dbContext.Set<ApplicationRole>().FindAsync([roleId], cancellationToken);
                if (role is not null && role.Status is not (RoleStatus.Active or RoleStatus.PendingApproval))
                {
                    return (false, $"角色 [{role.Name}] 目前為「{role.Status.GetDisplayName()}」，無法異動權限設定。");
                }

                var grant = await _dbContext.Set<PermissionGrant>()
                    .FirstOrDefaultAsync(x => x.RoleId == roleId && x.LabId == labId, cancellationToken);

                int maxBitIndex = request.GrantedBitPositions.Any() ? request.GrantedBitPositions.Max() : 0;
                int requiredBytes = Math.Max(64, (maxBitIndex / 8) + 1);

                byte[] newVector = new byte[requiredBytes];

                foreach (int bitPos in request.GrantedBitPositions)
                {
                    if (bitPos < 0) continue;

                    int byteIdx = bitPos / 8;
                    int bitOffset = bitPos % 8;

                    if (byteIdx >= newVector.Length)
                    {
                        Array.Resize(ref newVector, byteIdx + 1);
                    }

                    newVector[byteIdx] |= (byte)(1 << bitOffset);
                }

                if (grant == null)
                {
                    grant = new PermissionGrant
                    {
                        Id = Guid.NewGuid(),
                        RoleId = roleId,
                        LabId = labId,
                        PermissionVector = newVector
                    };

                    await _dbContext.Set<PermissionGrant>().AddAsync(grant, cancellationToken);
                }
                else
                {
                    grant.PermissionVector = newVector;
                    _dbContext.Set<PermissionGrant>().Update(grant);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("已成功儲存角色 [{RoleId}] 於實驗室 [{LabId}] 的 BitMask 向量設定。", roleId, labId);
                // 角色成員於此實驗室的有效權限已改變：清除快取並即時通知前端
                if (role?.Name is not null)
                {
                    await _permissionChangeNotifier.NotifyRoleMembersAsync(role.Name, "role-lab-permissions-updated", cancellationToken).ConfigureAwait(false);

                    var memberCount = await _dbContext.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>()
                        .CountAsync(x => x.RoleId == roleId, cancellationToken).ConfigureAwait(false);
                    await _roleAuditService.RecordAsync(roleId, role.Name, RoleAuditActions.PermissionsUpdated,
                        $"實驗室權限調整（實驗室 {labId}）：共授予 {request.GrantedBitPositions.Count()} 項，影響 {memberCount} 位成員",
                        memberCount, new { LabId = labId, request.GrantedBitPositions }, cancellationToken).ConfigureAwait(false);
                }

                return (true, "權限配置更新成功。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新角色實驗室權限時發生未預期異常。RoleId: {RoleId}, LabId: {LabId}", roleId, labId);
                return (false, "系統內部錯誤，更新失敗。");
            }
        }
    }
}