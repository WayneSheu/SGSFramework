// 檔案路徑: Abstractions/SGSFramework.AuthTokenBucket.Abstractions/IPermissionManagementService.cs

using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionAudits;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionGrants;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionTree;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionUsers;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.RolePermissions;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.UserPermissions;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    public interface IPermissionManagementService
    {
        //使用者權限點查詢 (基於 UserId 檢索資料庫/快取)
        Task<bool> HasPermissionAsync(string userId, string permissionCode, CancellationToken cancellationToken = default);

        //角色權限授與與撤銷 (Write Operations)
        Task GrantPermissionToRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default);
        
        //角色權限授與與撤銷 (Read Operations)
        Task RevokePermissionFromRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default);

        //後台 UI 權限矩陣與樹狀結構維護 (Management DTOs)
        /// <summary>
        /// 同步並更新系統 PermissionMetadata 資料表，並透過 DTO 投影回傳完整資料清單
        /// </summary>
        Task<IReadOnlyCollection<PermissionMetadataDto>> SyncPermissionMetadataAsync(CancellationToken cancellationToken = default);
        
        //後台 UI 權限矩陣與樹狀結構維護 (Management DTOs)
        Task<List<PermissionModuleDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 編輯單一權限項的權限標題 (PermissionTitle) 與說明 (Description)，並標記為已自訂使啟動同步不覆蓋。
        /// 動作標題 (ActionTitle) 取自程式碼，不可修改。同一控制器下共用同一權限鍵的多筆中繼資料會一併更新。
        /// </summary>
        /// <param name="permissionKey">權限鍵值</param>
        /// <param name="controllerName">所屬控制器名稱 (權限鍵可能被多個控制器共用，需一併指定)</param>
        /// <param name="permissionTitle">權限標題 (必填)</param>
        /// <param name="description">說明</param>
        /// <param name="cancellationToken">取消權牌</param>
        /// <returns>更新後的權限項資料</returns>
        Task<PermissionActionDto> UpdatePermissionMetadataAsync(
            string permissionKey,
            string controllerName,
            string permissionTitle,
            string? description,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 獲取角色權限矩陣 (Read Operations)
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<RolePermissionMatrixDto?> GetRoleGlobalPermissionsAsync(string roleId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 更新角色全域權限 (Write Operations)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<(bool Succeeded, string Message)> UpdateRolePermissionsAsync(UpdateRoleGlobalPermissionsRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得指定使用者的所有權限總覽 (包含從 User_Global_Permissions / Lab Permissions Bitmask 解碼之直接權限與角色繼承權限)
        /// </summary>
        Task<UserAuditPermissionsResponseDto?> GetUserAllPermissionsAsync(string userId, Guid? tenantLabId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 指派或更新指定使用者的直接 API 權限清單
        /// </summary>
        Task<(bool Succeeded, string Message)> AssignUserPermissionsAsync(string userId,Guid? tenantLabId,AssignUserPermissionsRequest request,CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得具備指定 PermissionKey 的所有使用者清單
        /// </summary>
        Task<PermissionUsersMasterDto> GetUsersByPermissionKeyAsync(string permissionKey,Guid? tenantLabId = null,CancellationToken cancellationToken = default);
    


    }
}