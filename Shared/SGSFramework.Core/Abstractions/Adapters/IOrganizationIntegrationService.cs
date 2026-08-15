using SGSFramework.Core.DTOs;

namespace SGSFramework.Core.Abstractions.Adapters;


/// <summary>
/// 跨模組組織節點資訊契約 DTO
/// </summary>
/// <param name="Id"></param>
/// <param name="Name"></param>
/// <param name="NodePathString"></param>
/// <param name="HierarchyLevel"></param>
public record OrganizationInfoContract(Guid Id, string Name, string NodePathString, int HierarchyLevel);

/// <summary>
/// 跨模組組織資料整合服務介面 (防腐層適配器 Adapter)
/// </summary>
/// <summary>
/// 跨模組組織資料整合服務介面 (防腐層適配器 Adapter)
/// </summary>
public interface IOrganizationIntegrationService
{
    /// <summary>
    /// 獲取單一組織節點資訊
    /// </summary>
    Task<OrganizationInfoContract?> GetOrganizationByIdAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證目標組織是否在使用者的管轄/存取組織階層範疇內
    /// </summary>
    Task<bool> IsInUserScopeAsync(
        string userId,
        Guid targetOrgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 獲取使用者有權存取的所有組織節點清單
    /// </summary>
    Task<List<OrganizationInfoContract>> GetUserAccessibleOrganizationsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 獲取使用者在指定實驗室的權限設定檔
    /// </summary>
    Task<UserPermissionProfileDto?> GetUserLabProfileAsync(
        string userId,
        Guid targetLabId,
        CancellationToken cancellationToken = default);
}