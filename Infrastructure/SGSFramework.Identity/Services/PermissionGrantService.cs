using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Identities;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using SGSFramework.Identity.DTOs.PermissionGrants;

namespace SGSFramework.Identity.Services
{
    /// <summary>
    /// 角色實驗室維度 BitMask 權限管理服務實作 (泛型 DbContext 綁定)
    /// </summary>
    public sealed class PermissionGrantService<TContext> : IPermissionGrantService
        where TContext : DbContext
    {
        private readonly TContext _dbContext;
        private readonly ILogger<PermissionGrantService<TContext>> _logger;

        public PermissionGrantService(
            TContext dbContext, // 要求具體的 TContext 泛型型態
            ILogger<PermissionGrantService<TContext>> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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