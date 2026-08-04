using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Modules
{
    /// <summary>
    /// 介面：模組實體，提供模組名稱屬性
    /// </summary>
    public interface IControllerMetadata
    {

        Guid Id { get; set; }
        string Version { get; set; }

        string ModuleName { get; set; }//模組名稱屬性，用於標識實體所屬的模組
        string ControllerName { get; set; }
        string ControllerTypeName { get; set; }//控制器類型名稱屬性，用於標識實體所對應的控制器類型名稱
        string Description { get; set; }
        string ActionName { get; set; }  
        string DisplayName { get; set; } 
        string RouteTemplate { get; set; }
        string PermissionKey { get; set; }
        string? Icon { get; set; }
        int DisplayOrder { get; set; }
        string? ParentMenuName { get; set; }
        bool IsActive { get; set; }

    }
}
