using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Persistent.Abstractions.ScriptRunners
{
    /// <summary>
    /// 自動腳本執行器介面, 用於初始化資料庫
    /// </summary>
    public interface IDatabaseInitializer
    {
        /// <summary>
        /// 執行資料庫 Schema、使用者權限及動態腳本初始化。
        /// </summary>
        /// <param name="cancellationToken">取消權杖。</param>
        /// <returns>非同步任務。</returns>
        Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);

    }
}
