using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Permissions.Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Services
{
    public sealed class LaboratoryAccessService : ILaboratoryAccessService
    {
        private readonly ILogger<LaboratoryAccessService> _logger;
        private readonly IUserLabRepository _userLabRepository;
        private readonly ILaboratoryMetadataProvider _metadataProvider;

        public LaboratoryAccessService(
            ILogger<LaboratoryAccessService> logger,
            IUserLabRepository userLabRepository,
            ILaboratoryMetadataProvider metadataProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userLabRepository = userLabRepository ?? throw new ArgumentNullException(nameof(userLabRepository));
            _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        }

        public async Task<bool> ValidateUserLaboratoryAccessAsync(Guid userId, Guid tenantLabId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(userId);
            ArgumentNullException.ThrowIfNull(tenantLabId);

            try
            {
                // 使用共用介面取得使用者可訪問的實驗室清單 (對應 UserLabMapping)
                var accessibleLabs = await _userLabRepository.GetAccessibleLabsAsync(userId, cancellationToken);

                // 比對 TenantLabId 並檢查時效與啟用狀態
                return accessibleLabs.Any(lab => lab.TenantLabId == tenantLabId && lab.IsValidAt(DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LaboratoryAccessService] 驗證使用者實驗室存取權限異常。UserId: {UserId}, TenantLabId: {TenantLabId}", userId, tenantLabId);
                throw;
            }
        }

        public async Task<bool> ValidateLaboratoryCategoryAsync(Guid tenantLabId, string[] allowedCategories, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tenantLabId);

            if (allowedCategories == null || allowedCategories.Length == 0)
            {
                return true;
            }

            try
            {
                var labMeta = await _metadataProvider.GetLaboratoryMetadataAsync(tenantLabId, cancellationToken);
                if (labMeta is null || !labMeta.IsActive)
                {
                    return false;
                }

                foreach (var category in allowedCategories)
                {
                    if (string.Equals(labMeta.Category, category, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(labMeta.DepartmentCode, category, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(labMeta.Code) && labMeta.Code.StartsWith(category, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LaboratoryAccessService] 驗證實驗室類別異常。TenantLabId: {TenantLabId}", tenantLabId);
                throw;
            }
        }
    }
}
