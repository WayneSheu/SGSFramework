// ==========================================
// 檔案路徑: src/SGSFramework.Identity/Options/SystemRolePermissionSeedOptions.cs
// 架構層級: Identity / Options
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Options
{
    using System.Collections.Generic;

    public class SystemRolePermissionSeedOptions
    {
        public const string SectionName = "SystemRolePermissionSeed";

        /// <summary>
        /// 角色權限派發規則清單
        /// </summary>
        public List<RolePermissionAssignmentRule> Roles { get; set; } = new();
    }

    public class RolePermissionAssignmentRule
    {
        /// <summary>
        /// 目標系統角色名稱 (例如: SuperAdmin, LabManager)
        /// </summary>
        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// 角色代碼 (例如: ROLE_SUPER_ADMIN, ROLE_LAB_MANAGER)
        /// </summary>
        public string RoleCode { get; set; } = string.Empty;

        /// <summary>
        /// 職責說明 (例如: 系統技術最高管理員：僅負責系統部署...)
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 對應預設權限型態 / 範圍說明 (例如: 全域最高權限、實驗室管理、唯讀稽核等)
        /// </summary>
        public List<string> DefaultPermissionTypes { get; set; } = new();

        /// <summary>
        /// 是否指派所有有效權限 (適用於 SuperAdmin)
        /// </summary>
        public bool AssignAll { get; set; } = false;

        /// <summary>
        /// 包含的權限 Key 或萬用字元前綴 (例如: "ORG.*", "SYSTEM.USERMANAGEMENT.READ")
        /// </summary>
        public List<string> IncludeKeys { get; set; } = new();

        /// <summary>
        /// 包含的權限敏感度類別 (對應 ActionCategory，例如: "Operational", "Basic")
        /// </summary>
        public List<string> Categories { get; set; } = new();

        /// <summary>
        /// 排除的權限 Key 或條件
        /// </summary>
        public List<string> ExcludeKeys { get; set; } = new();
    }
}