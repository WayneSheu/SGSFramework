using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ApiInfrastructure.Extensions
{
    /// <summary>
    /// 資料庫自動遷移擴充工具
    /// </summary>
    public static class DatabaseMigrationExtensions
    {
        /// <summary>
        /// 自動套用指定的 EF Core Migration 至資料庫
        /// </summary>
        public static async Task MigrateDatabaseAsync<TDbContext>(this IServiceProvider serviceProvider)
            where TDbContext : DbContext
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            using var scope = serviceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<TDbContext>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

            try
            {
                logger.LogInformation(">>> 開始檢查並執行資料庫 Migration (Context: {DbContextName})...", typeof(TDbContext).Name);

                // 確保資料庫與對應 Schema 建立，並執行所有未套用的 Migrations
                await dbContext.Database.MigrateAsync();

                logger.LogInformation(">>> 資料庫 Migration 執行成功，所有綱要與資料表已同步。");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ">>> 執行資料庫 Migration 時發生嚴重錯誤: {Message}", ex.Message);
                throw new InvalidOperationException($"資料庫自動遷移失敗: {ex.Message}", ex);
            }
        }
    }
}
