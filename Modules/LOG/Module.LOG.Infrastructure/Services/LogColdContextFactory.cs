using Microsoft.EntityFrameworkCore;
using SGS.Modules.LOG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Logging;

namespace SGS.Modules.LOG.Infrastructure.Services;

/// <summary>
/// 建立冷庫（年度換庫後的唯讀歷史日誌庫）的 <see cref="LogDbContext"/>。
/// 連線字串由 <see cref="ILogTierCatalog"/> 以 LogConnection 替換資料庫名稱產生；一律 NoTracking，呼叫端負責釋放。
/// 冷庫為 READ_ONLY，不套用 Migration、不寫入。
/// </summary>
public sealed class LogColdContextFactory(ILogTierCatalog catalog)
{
    public LogDbContext Create(string? tierKey)
    {
        var tier = catalog.Find(tierKey) ?? throw new ArgumentException($"找不到日誌資料庫來源：{tierKey}", nameof(tierKey));

        var options = new DbContextOptionsBuilder<LogDbContext>()
            .UseSqlServer(catalog.GetConnectionString(tier.Key), sql =>
            {
                sql.MigrationsAssembly(typeof(LogDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "log");
            })
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new LogDbContext(options);
    }
}
