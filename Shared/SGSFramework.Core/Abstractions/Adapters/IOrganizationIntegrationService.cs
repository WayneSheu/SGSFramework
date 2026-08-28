using SGSFramework.Core.DTOs;

namespace SGSFramework.Core.Abstractions.Adapters;


/// <summary>
/// 跨模組傳遞之組織/實驗室資料契約 (包含主實驗室標籤與啟用狀態)
/// </summary>
public record OrganizationInfoContract
{
    public int Id { get; init; }
    public Guid TenantLabId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string NodePathString { get; init; } = string.Empty;
    public int HierarchyLevel { get; init; }
    public bool IsPrimary { get; init; }
    public bool IsActive { get; init; } = true;
    public int? ParentLabId { get; init; }
    public Guid? ParentTenantLabId { get; init; }
    public string? ParentLabCode { get; init; }
    public string? ParentLabName { get; init; }
}

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

    /// <summary>
    /// 獲取全系統所有未刪除且有效的區域實驗室/組織 (專供系統管理員特權開路呼叫)
    /// </summary>
    Task<List<OrganizationInfoContract>> GetAllActiveOrganizationsAsync(
        CancellationToken cancellationToken = default);

}