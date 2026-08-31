using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 模組權限樹狀群組 DTO（供前端角色/使用者權限勾選樹狀結構使用）
    /// </summary>
    public sealed class ModulePermissionGroupDto
    {
        public string ModuleName { get; init; } = string.Empty;
        public string ModuleTitle { get; init; } = string.Empty;
        public List<ControllerPermissionGroupDto> Controllers { get; init; } = new();
    }
}
