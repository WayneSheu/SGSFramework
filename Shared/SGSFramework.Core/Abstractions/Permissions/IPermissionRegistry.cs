using SGSFramework.Core.Abstractions.Permissions.Identities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions
{
    /// <summary> 
    /// 定義權限註冊表的合約，該註冊表管理權限代碼及其對應的位元遮罩值。
    /// </summary>
    /// <summary>
    /// 定義動態權限註冊表之企業級合約，管理系統 Permission Entity 中繼資料與 BitPosition 映射關係。
    /// </summary>
    public interface IPermissionRegistry
    {
        /// <summary>
        /// 取得或建立指定權限代碼的 BitPosition 索引位置 (支援突破 64 位元限制)。
        /// </summary>
        /// <param name="permissionKey">權限代碼 (例如: "ORG_LAB_READ")</param>
        /// <returns>位元索引位置</returns>
        int GetOrCreateBitPosition(string permissionKey);

        /// <summary>
        /// 取得所有已註冊之權限 Entity 完整清單
        /// </summary>
        /// <returns>唯讀 Permission 實體集合</returns>
        IReadOnlyCollection<Permission> GetAllPermissions();

        /// <summary>
        /// 取得權限代碼 (PermissionKey) 與位元位置 (BitPosition) 之對照字典
        /// </summary>
        /// <returns>唯讀字典集合 (Key: PermissionKey, Value: BitPosition)</returns>
        IReadOnlyDictionary<string, int> GetAllMappings();

        /// <summary>
        /// 依據權限代碼取得單一 Permission 實體
        /// </summary>
        /// <param name="permissionKey">權限代碼</param>
        /// <param name="permission">Permission 實體輸出</param>
        /// <returns>若存在則傳回 true</returns>
        bool TryGetPermission(string permissionKey, out Permission? permission);
    }
}
