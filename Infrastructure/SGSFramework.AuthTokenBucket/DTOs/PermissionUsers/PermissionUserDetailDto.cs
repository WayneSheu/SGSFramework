
// ==========================================
// 檔案路徑: Application/SGSFramework.AuthTokenBucket/DTOs/UserPermissions/PermissionUserDetailDto.cs
// 架構層級: Application DTO Layer
// ==========================================

#nullable enable

using SGSFramework.AuthTokenBucket.DTOs.PermissionUsers;
namespace SGSFramework.AuthTokenBucket.DTOs.UserPermissions;

/// <summary>
/// Detail: 使用者詳細資訊與權限判定來源
/// </summary>
public class PermissionUserDetailDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 是否具備使用者層級的直接權限
        /// </summary>
        public bool HasDirectPermission { get; set; }

        /// <summary>
        /// 賦予該權限的角色清單
        /// </summary>
        public List<string> GrantedByRoles { get; set; } = new();

        /// <summary>
        /// 使用者實驗室關聯清單
        /// </summary>
        public List<UserLabDto> LabMappings { get; set; } = new();

        /// <summary>
        /// 使用者資料庫真實存放的 Bitmask 數值
        /// </summary>
        public long RawUserBitmask { get; set; }

        /// <summary>
        /// 角色驗證細節 (角色名稱 -> 角色資料庫 Bitmask 數值)
        /// </summary>
        public Dictionary<string, long> RoleBitmaskDetails { get; set; } = new();
    }

