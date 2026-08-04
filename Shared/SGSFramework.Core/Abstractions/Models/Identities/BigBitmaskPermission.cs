using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Models.Identities
{
    /// <summary>
    /// 支援超過 64 位元的大容量複合 Bitmask 權限計算載體（解決超限權限點遮罩運算）
    /// </summary>
    public sealed class BigBitmaskPermission
    {
        // 每組元素代表 64 個權限位元
        private readonly List<long> _masks = new();

        public BigBitmaskPermission(IEnumerable<long> masks)
        {
            if (masks != null)
            {
                _masks.AddRange(masks);
            }
        }

        /// <summary>
        /// 將特定權限點編號（由 0 開始）設定進對應的位元桶中
        /// </summary>
        public void SetPermission(int permissionId)
        {
            if (permissionId < 0) throw new ArgumentOutOfRangeException(nameof(permissionId));

            int bucketIndex = permissionId / 64;
            int bitIndex = permissionId % 64;

            while (_masks.Count <= bucketIndex)
            {
                _masks.Add(0L);
            }

            _masks[bucketIndex] |= (1L << bitIndex);
        }

        /// <summary>
        /// 匯出可供序列化或寫入 Claim 的 Long 陣列
        /// </summary>
        public long[] ToArray() => _masks.ToArray();

        /// <summary>
        /// 轉為逗號分隔的字串，便於 JWT Claim 的高維度儲存
        /// </summary>
        public override string ToString() => string.Join(",", _masks);
    }
}
