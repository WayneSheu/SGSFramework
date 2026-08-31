using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 控制器權限群組 DTO
    /// </summary>
    public sealed class ControllerPermissionGroupDto
    {
        public string ControllerName { get; init; } = string.Empty;
        public string ControllerTitle { get; init; } = string.Empty;
        public string? Icon { get; init; }
        public int DisplayOrder { get; init; }
        public List<ControllerMetadataDto> Actions { get; init; } = new();
    }
}
