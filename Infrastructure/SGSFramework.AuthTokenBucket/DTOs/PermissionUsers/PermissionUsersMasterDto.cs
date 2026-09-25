using SGSFramework.AuthTokenBucket.DTOs.UserPermissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionUsers;

    /// <summary>
    /// 具備特定權限的使用者清單 (Master-Details 結構：權限主檔與使用者明細)
    /// </summary>
    public class PermissionUsersMasterDto
    {
        // ==========================================
        // Master: 權限資訊
        // ==========================================
        public string ModuleName { get; set; } = string.Empty;
        public string ModuleTitle { get; set; } = string.Empty;
        public string FunctionTitle { get; set; } = string.Empty;
        public string PermissionTitle { get; set; } = string.Empty;
        public string PermissionDescription { get; set; } = string.Empty;
        public int BitPosition { get; set; }
        public long TargetBitmaskFlag { get; set; }

        /// <summary>
        // Details: 具備此權限的使用者清單
        /// </summary>
        public List<PermissionUserDetailDto> Users { get; set; } = new();
    
}
