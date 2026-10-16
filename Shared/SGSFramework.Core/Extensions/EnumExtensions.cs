using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;

namespace SGSFramework.Core.Extensions
{
    public static class EnumExtensions
    {
        // 企業級效能優化：使用安全執行緒的 ConcurrentDictionary 快取反射結果，避免重複反射影響系統效能
        private static readonly ConcurrentDictionary<Enum, string> DisplayNameCache = new();

        /// <summary>
        /// 取得列舉值（Enum）上的 [Display(Name = "...")] 名稱。
        /// 若未設定 Display 屬性，則自動回退至列舉的字串名稱 (ToString())。
        /// </summary>
        public static string GetDisplayName(this Enum? value)
        {
            if (value == null) return string.Empty;

            return DisplayNameCache.GetOrAdd(value, enumVal =>
            {
                var type = enumVal.GetType();
                var member = type.GetMember(enumVal.ToString());

                if (member.Length > 0)
                {
                    var displayAttribute = member[0].GetCustomAttribute<DisplayAttribute>();
                    if (displayAttribute != null && !string.IsNullOrWhiteSpace(displayAttribute.Name))
                    {
                        return displayAttribute.Name;
                    }
                }

                return enumVal.ToString();
            });
        }
    }
}
