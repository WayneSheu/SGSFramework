using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Database
{
    /// <summary>
    /// 資料庫腳本執行策略介面 (Strategy Pattern)
    /// </summary>
    public interface IScriptExecutionStrategy
    {
        /// <summary>
        /// 執行指定之 SQL 腳本檔案
        /// </summary>
        /// <param name="connectionString">資料庫連線字串</param>
        /// <param name="filePath">SQL 腳本檔案絕對路徑</param>
        /// <param name="cancellationToken">取消標記</param>
        Task ExecuteScriptAsync(string connectionString, string filePath, CancellationToken cancellationToken = default);
    }
}
