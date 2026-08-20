using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Generators
{
    /// <summary>
    /// 全域唯一識別碼 (GUID/UUID) 產生器介面
    /// </summary>
    public interface IGuidGenerator
    {
        /// <summary>
        /// 產生具備時間排序特性的 UUIDv7 (以當前 UTC 時間為基準)
        /// </summary>
        /// <returns>具備時間排序特性的 Guid</returns>
        Guid CreateVersion7();

        /// <summary>
        /// 依據指定時間戳記產生 UUIDv7
        /// </summary>
        /// <param name="timestamp">指定的時間戳記</param>
        /// <returns>具備時間排序特性的 Guid</returns>
        Guid CreateVersion7(DateTimeOffset timestamp);
    }
}
