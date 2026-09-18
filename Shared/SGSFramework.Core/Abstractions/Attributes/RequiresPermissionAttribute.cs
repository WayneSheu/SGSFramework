// ==========================================
// 檔案路徑: src/SGSFramework.Core.Abstractions/Attributes/RequiresPermissionAttribute.cs
// 架構層級: Core Abstractions / Attributes Layer
// ==========================================

using System;

namespace SGSFramework.Core.Abstractions.Attributes
{
 
    /// 標註於 Controller 或 Action 上，用於宣告所需的權限 Key 與選擇性的權限顯示標題 (PermissionTitle)
    /// 
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RequiresPermissionAttribute : Attribute
    {
        /// 
        /// 權限唯一識別碼 (PermissionKey)
        /// 
        public string PermissionKey { get; }

        /// <summary>
        /// 權限顯示標題 (PermissionTitle)，若指定則優先作為資料庫中 PermissionTitle 的來源
        /// </summary>
        public string? PermissionTitle { get; }

        /// <summary>
        /// 初始化 <see cref="RequiresPermissionAttribute"/> 類別的新實例，並指定所需的權限鍵。
        /// </summary>
        /// <param name="key">權限鍵名稱</param>
        public RequiresPermissionAttribute(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            PermissionKey = key;
        }

        /// <summary>
        /// 初始化 <see cref="RequiresPermissionAttribute"/> 類別的新實例，並指定權限鍵與前端顯示標題。
        /// </summary>
        /// <param name="key">權限鍵名稱</param>
        /// <param name="permissionTitle">權限顯示標題</param>
        public RequiresPermissionAttribute(string key, string permissionTitle)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            PermissionKey = key;
            PermissionTitle = permissionTitle;
        }
    }
}