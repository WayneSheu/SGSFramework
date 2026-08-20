using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Generators
{
    /// <summary>
    /// 系統標準 GUID 產生器實作 (以 Singleton 形式註冊，完全相容 ALC 動態外掛與反射機制)
    /// </summary>
    public sealed class SystemGuidGenerator : IGuidGenerator
    {
        /// <summary>
        /// 安全生成時間有序之 UUIDv7 (明確帶入 UtcNow 以避免多載靜態反射簽名不匹配)
        /// </summary>
        /// <returns>Guid</returns>
        /// <exception cref="InvalidOperationException">當底層生成 UUID 發生異常時拋出</exception>
        public Guid CreateVersion7()
        {
            return CreateVersion7(DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// 依據指定時間戳記安全生成 UUIDv7
        /// </summary>
        /// <param name="timestamp">指定時間戳</param>
        /// <returns>Guid</returns>
        /// <exception cref="InvalidOperationException">當底層生成 UUID 發生異常時拋出</exception>
        public Guid CreateVersion7(DateTimeOffset timestamp)
        {
            try
            {
                // 顯式呼叫帶有 DateTimeOffset 參數的重載，排除反射模糊問題
                return Guid.CreateVersion7(timestamp);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("生成 UUIDv7 發生系統內部錯誤。", ex);
            }
        }
    }
}
