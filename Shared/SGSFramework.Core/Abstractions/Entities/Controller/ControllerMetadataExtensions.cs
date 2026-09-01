// ==========================================
// 檔案路徑: src/SGSFramework/Core/Abstractions/Entities/Controller/ControllerMetadataExtensions.cs
// 架構層級: Core Abstractions Layer (無須參考 AuthTokenBucket)
// ==========================================

using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace SGSFramework.Core.Abstractions.Entities.Controller
{
    public static class ControllerMetadataExtensions
    {
        public static void SyncFromAttribute(this IControllerMetadata entity, Type controllerType)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(controllerType);

            var menuAttr = controllerType.GetCustomAttribute<MenuAttribute>();
            MethodInfo? method = null;

            if (!string.IsNullOrEmpty(entity.ActionName))
            {
                method = controllerType.GetMethod(entity.ActionName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
                if (menuAttr == null)
                {
                    menuAttr = method?.GetCustomAttribute<MenuAttribute>();
                }
            }

            if (menuAttr != null)
            {
                entity.DisplayName = menuAttr.Name;
                entity.Icon = menuAttr.Icon;
                entity.DisplayOrder = menuAttr.Order;
                entity.ParentMenuName = menuAttr.Parent;
            }

            var bitPosAttr = method?.GetCustomAttribute<BitPositionAttribute>() ?? controllerType.GetCustomAttribute<BitPositionAttribute>();
            if (bitPosAttr != null)
            {
                entity.BitPosition = bitPosAttr.Position;
            }

            // 利用 CustomAttributeData 跨越組件邊界動態讀取 AuthTokenBucket 的屬性參數
            var customAttrsInfo = new Dictionary<string, object>();

            var allAttributeData = new List<CustomAttributeData>();
            if (controllerType != null)
            {
                allAttributeData.AddRange(controllerType.GetCustomAttributesData());
            }
            if (method != null)
            {
                allAttributeData.AddRange(method.GetCustomAttributesData());
            }

            List<string>? labCategories = null;
            List<string>? reportCategories = null;

            foreach (var attrData in allAttributeData)
            {
                var attrName = attrData.AttributeType.Name;

                // 抓取 RequireLaboratoryAttribute（改用純字串比對，避開 Core 層無法參考 AuthTokenBucket 的型別限制）
                if (attrName == "RequireLaboratoryAttribute")
                {
                    labCategories = ExtractCategoriesFromAttributeData(attrData);
                }
                // 抓取 RequireReportCategoryAttribute
                else if (attrName == "RequireReportCategoryAttribute")
                {
                    reportCategories = ExtractCategoriesFromAttributeData(attrData);
                }
            }

            if (labCategories is { Count: > 0 })
            {
                customAttrsInfo["RequireLaboratory"] = labCategories;
            }

            if (reportCategories is { Count: > 0 })
            {
                customAttrsInfo["RequireReportCategory"] = reportCategories;
            }

            if (customAttrsInfo.Count > 0)
            {
                entity.AttributesJson = JsonSerializer.Serialize(customAttrsInfo, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
        }

        private static List<string> ExtractCategoriesFromAttributeData(CustomAttributeData attrData)
        {
            var categories = new List<string>();
            if (attrData.ConstructorArguments.Count > 0)
            {
                var firstArg = attrData.ConstructorArguments[0];
                // TypeFilterAttribute 透過 params 傳入的陣列會包裝在 CustomAttributeTypedArgument 的 Collection 中
                if (firstArg.Value is IReadOnlyCollection<CustomAttributeTypedArgument> argCollection)
                {
                    foreach (var item in argCollection)
                    {
                        if (item.Value is string val)
                        {
                            categories.Add(val);
                        }
                    }
                }
            }
            return categories;
        }
    }
}