using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ApiInfrastructure.DTOs
{
    /// <summary>
    /// 系統控制器中繼資料傳輸物件（對應完整 ControllerMetadata 實體屬性）
    /// </summary>
    public sealed class ControllerMetadataDto
    {
        public Guid Id { get; init; }
        public string Version { get; init; } = "v1";
        public string ModuleName { get; init; } = string.Empty;
        public string ModuleTitle { get; init; } = string.Empty;
        public string ControllerTitle { get; init; } = string.Empty;
        public string ControllerName { get; init; } = string.Empty;
        public string ControllerTypeName { get; init; } = string.Empty;
        public string ActionName { get; init; } = string.Empty;
        public string RouteTemplate { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string? Icon { get; init; }
        public int DisplayOrder { get; init; }
        public string? ParentMenuName { get; init; }
        public string PermissionKey { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }
}
