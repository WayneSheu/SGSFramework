using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Controller
{
    public static class ControllerMetadataExtensions
    {
        public static void SyncFromAttribute(this IControllerMetadata entity, Type controllerType)
        {
            // 優先嘗試從類別層級獲取 MenuAttribute[cite: 1]
            var menuAttr = controllerType.GetCustomAttribute<MenuAttribute>();

            // 若找不到類別定義，則嘗試從對應的 Action 方法找[cite: 1]
            if (menuAttr == null && !string.IsNullOrEmpty(entity.ActionName))
            {
                var method = controllerType.GetMethod(entity.ActionName);
                menuAttr = method?.GetCustomAttribute<MenuAttribute>();
            }

            if (menuAttr != null)
            {
                entity.DisplayName = menuAttr.Name;
                entity.Icon = menuAttr.Icon;
                entity.DisplayOrder = menuAttr.Order;
                entity.ParentMenuName = menuAttr.Parent;
            }
        }
    }
}
