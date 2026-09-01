namespace SGSFramework.AuthTokenBucket.Services;

using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Adapters;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

public sealed class LaboratoryAuthorizationService(
    IUserRuntimeScopeService runtimeScopeService,
    IOrganizationIntegrationService orgIntegrationService) : ILaboratoryAuthorizationService
{
    private readonly IUserRuntimeScopeService _runtimeScopeService = runtimeScopeService ?? throw new ArgumentNullException(nameof(runtimeScopeService));
    private readonly IOrganizationIntegrationService _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));

    public async Task<bool> ValidateUserLaboratoryCategoryAsync(ClaimsPrincipal user, string[] allowedCategories, CancellationToken cancellationToken)
    {
        if (user?.Identity is not { IsAuthenticated: true } || allowedCategories == null || allowedCategories.Length == 0)
        {
            return false;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        // 1. 取得當前使用者鎖定或預設的實驗室清單
        var accessibleLabs = await _runtimeScopeService.GetAccessibleLabsAsync(userId, cancellationToken);
        var activeLab = accessibleLabs.FirstOrDefault(l => l.IsPrimary) ?? accessibleLabs.FirstOrDefault();

        if (activeLab == null || activeLab.TenantLabId == Guid.Empty)
        {
            return false;
        }

        // 2. 透過 IOrganizationIntegrationService 取得組織節點與父層代碼，完全不直接相依 ORG 模組的 DbContext
        var orgInfo = await _orgIntegrationService.GetOrganizationByIdAsync(activeLab.TenantLabId, cancellationToken);

        if (orgInfo == null || string.IsNullOrWhiteSpace(orgInfo.ParentLabCode))
        {
            return false;
        }

        // 3. 檢查 ParentLabCode 是否符合允許的區域實驗室類別 (例如 ME 或 SL)
        return allowedCategories.Contains(orgInfo.ParentLabCode, StringComparer.OrdinalIgnoreCase);
    }
}