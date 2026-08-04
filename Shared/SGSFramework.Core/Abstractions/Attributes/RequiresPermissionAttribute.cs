using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Attributes
{
    /// <summary>
    /// 標記控制器或方法的屬性，用於指定所需的權限鍵。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequiresPermissionAttribute : Attribute
    {
        /// <summary>
        /// 權限鍵, 用於指定該控制器或方法所需的權限。
        /// </summary>
        public string PermissionKey { get; }

        /// <summary>
        /// 初始化 RequiresPermissionAttribute 類的新實例，並指定所需的權限鍵。
        /// </summary>
        /// <param name="key"></param>
        public RequiresPermissionAttribute(string key) => PermissionKey = key;
   
    }
}
