using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    /// <summary>
    /// 具備特定權限的使用者資訊 DTO
    /// </summary>
    public class PermissionUserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 是否具備使用者層級的直接權限
        /// </summary>
        public bool HasDirectPermission { get; set; }

        public string ModuleTitle { get; set; } = string.Empty;

        public string FunctionTitle { get; set; } = string.Empty;

        public string PermissionTitle { get; set; }=string.Empty;

        public string PermissionDescription { get; set;  } = string.Empty;

        /// <summary>
        /// 賦予該權限的角色清單
        /// </summary>
        public List<string> GrantedByRoles { get; set; } = new();

        // ==========================================
        // 驗證與稽核專用欄位 (Audit/ Inspection)
        // ==========================================
        /// <summary>
        /// 權限模組名稱
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// 權限在模組中的 Bit 位元位置
        /// </summary>
        public int BitPosition { get; set; }

        /// <summary>
        /// 當前比對的 Bitmask 旗標值 (1L << BitPosition)
        /// </summary>
        public long TargetBitmaskFlag { get; set; }

        /// <summary>
        /// 使用者資料庫真實存放的 Bitmask 數值
        /// </summary>
        public long RawUserBitmask { get; set; }

        /// <summary>
        /// 角色驗證細節 (角色名稱 -> 角色資料庫 Bitmask 數值)
        /// </summary>
        public Dictionary<string, long> RoleBitmaskDetails { get; set; } = new();
    }
}
