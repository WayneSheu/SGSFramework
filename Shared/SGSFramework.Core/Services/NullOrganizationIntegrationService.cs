using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.DTOs;

namespace SGSFramework.Core.Services;

/// <summary>
/// 組織整合服務之 Fallback (Null Object) 實作。
/// 當系統未載入 SGS.Modules.ORG 外掛模組時，由此服務接管，確保系統安全運行不崩潰。
/// </summary>
public sealed class NullOrganizationIntegrationService : IOrganizationIntegrationService
{
    private readonly ILogger<NullOrganizationIntegrationService>? _logger;

    public static NullOrganizationIntegrationService Instance { get; } = new NullOrganizationIntegrationService();

    public NullOrganizationIntegrationService() { }

    public NullOrganizationIntegrationService(ILogger<NullOrganizationIntegrationService> logger)
    {
        _logger = logger;
    }

    public Task<OrganizationInfoContract?> GetOrganizationByIdAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        LogWarning(nameof(GetOrganizationByIdAsync));
        return Task.FromResult<OrganizationInfoContract?>(null);
    }

    public Task<bool> IsInUserScopeAsync(string userId, Guid targetOrgId, CancellationToken cancellationToken = default)
    {
        LogWarning(nameof(IsInUserScopeAsync));
        // Fallback 策略：未載入組織模組時預設拒絕存取，確保資訊安全
        return Task.FromResult(false);
    }

    public Task<List<OrganizationInfoContract>> GetUserAccessibleOrganizationsAsync(string userId, CancellationToken cancellationToken = default)
    {
        LogWarning(nameof(GetUserAccessibleOrganizationsAsync));
        return Task.FromResult(new List<OrganizationInfoContract>());
    }

    /// <summary>
    /// 獲取全系統所有未刪除且有效的區域實驗室/組織 (Null Object Fallback 實作)
    /// </summary>
    public Task<List<OrganizationInfoContract>> GetAllActiveOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        LogWarning(nameof(GetAllActiveOrganizationsAsync));
        return Task.FromResult(new List<OrganizationInfoContract>());
    }

    public Task<UserPermissionProfileDto?> GetUserLabProfileAsync(
        string userId,
        Guid targetLabId,
        CancellationToken cancellationToken = default)
    {
        LogWarning(nameof(GetUserLabProfileAsync));
        return Task.FromResult<UserPermissionProfileDto?>(null);
    }

    private void LogWarning(string methodName)
    {
        _logger?.LogWarning(">>> [Fallback Warning] 呼叫了 NullOrganizationIntegrationService.{MethodName}。系統當前未綁定 SGS.Modules.ORG 模組實作。", methodName);
    }
}