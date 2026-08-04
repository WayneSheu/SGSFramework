using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Utilities
{
    /// <summary>
    /// 類型工具類 (Type Utilities)
    /// </summary>
    public static class TypeUtilities
    {
        public static string GetGenericTypeName(this Type type)
        {
            if (type.IsGenericType)
            {
                string genericTypes = string.Join(",", type.GetGenericArguments().Select(GetGenericTypeName).ToArray());
                return $"{type.Name.Remove(type.Name.IndexOf('`'))}<{genericTypes}>";
            }

            return type.Name;
        }
    }
}
