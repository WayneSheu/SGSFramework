using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Migrations
{
    /// <summary>
    /// 支援遷移檔案偵測的模組遷移服務介面
    /// </summary>
    public interface IMigrationService
    {
        /// <summary>
        /// 取得該模組目前所有已在本機組件中定義的 Migration 識別名稱清單
        /// </summary>
        IEnumerable<string> GetLocalMigrations();

        /// <summary>
        /// 取得該模組已套用至資料庫的 Migration 識別名稱清單
        /// </summary>
        Task<IEnumerable<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得該模組尚未套用至資料庫的 Migration 識別名稱清單（待處理項目）
        /// </summary>
        Task<IEnumerable<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 執行該模組的資料庫遷移
        /// </summary>
        Task MigrateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 執行診斷檢查，確認模組的遷移檔案與資料庫狀態是否一致，並列出任何不一致的項目
        /// </summary>
        /// <returns></returns>
        Task DiagnosticMigrations();
    }
}
