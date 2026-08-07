using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions
{
    /// <summary> 
    /// 定義權限註冊表的合約，該註冊表管理權限代碼及其對應的位元遮罩值。
    /// </summary>
    public interface IPermissionRegistry
    {
        /// <summary>
        /// 取得或建立指定權限代碼的位元遮罩值。
        /// </summary>
        /// <param name="permissionCode"></param>
        /// <returns></returns>
        long GetOrCreateMask(string permissionCode);

        /// <summary>
        /// 取得所有權限代碼及其對應的位元遮罩值。
        /// </summary>
        /// <returns></returns>
        IReadOnlyDictionary<string, long> GetAllMappings();


    }
}
