using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 權限位元遮罩 (Bitmask) 計算與轉譯服務介面
    /// </summary>
    public interface IPermissionBitmaskService
    {
        /// <summary>
        /// 將細粒度 PermissionKey 集合轉譯並歸類為各 Controller/Module 的 64 位元遮罩字典
        /// </summary>
        /// <param name="permissions">使用者或角色具備的 PermissionKey 集合</param>
        /// <param name="cancellationToken">取消權限 Token</param>
        /// <returns>Key 為 ControllerName/PermissionKey，Value 為對應的 Bitmask 值</returns>
        Task<Dictionary<string, long>> CalculateModuleBitmasksAsync(
            IEnumerable<string> permissions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 將資料庫中的 Feature Bitmask 字典還原解碼為完整的 Permission Key 清單
        /// </summary>
        Task<List<string>> DecodeBitmaskToPermissionsAsync(
            Dictionary<string, long> bitmaskMap,
            CancellationToken cancellationToken = default);
    }
}
