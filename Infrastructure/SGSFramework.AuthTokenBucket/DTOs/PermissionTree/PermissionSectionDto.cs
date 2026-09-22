using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionTree
{
    public class PermissionSectionDto
    {
        /// <summary>
        /// 區段識別名稱 (對應 ControllerMetadatas 的 ModuleName)
        /// </summary>
        public string SectionName { get; set; } = string.Empty;

        /// <summary>
        /// 區段顯示標題(對應 ControllerMetadatas 的 ModuleTitle)
        /// </summary>
        public string SectionTitle { get; set; } = string.Empty;

        /// <summary>
        /// 包含的模組清單 (對應 ControllerMetadatas 的 ControllerTitle)
        /// </summary>
        public List<PermissionModuleDto> Modules { get; set; } = new();
    }
}
