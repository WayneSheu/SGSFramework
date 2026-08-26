using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Attributes
{
    /// <summary>
    /// 標記控制器或方法的屬性，用於指定所需的權限鍵（純元資料宣告，不直接包含 Filter 執行邏輯）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class RequiresPermissionAttribute : Attribute
    {
        /// <summary>
        /// 權限鍵，用於指定該控制器或方法所需的權限鍵。
        /// </summary>
        public string PermissionKey { get; }

        /// <summary>
        /// 初始化 <see cref="RequiresPermissionAttribute"/> 類別的新實例，並指定所需的權限鍵[cite: 6]。
        /// </summary>
        /// <param name="key">權限鍵名稱[cite: 6]</param>
        public RequiresPermissionAttribute(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            PermissionKey = key;
        }
    }
}
