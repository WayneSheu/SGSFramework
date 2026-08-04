using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SGSFramework.Persistent.Extensions
{

    /// <summary>
    /// 這個類別提供了 DbContextOptionsBuilder 的擴充方法，用於注入自訂的 schema 配置。
    /// </summary>
    /// <remarks>These extension methods enable the injection of schema information into Entity Framework Core
    /// DbContext options. This allows derived DbContext types to retrieve the configured schema during construction,
    /// facilitating multi-schema or tenant-aware scenarios. The extensions follow the standard EF Core pattern for
    /// options immutability and extension management.</remarks>
    public static class DbContextOptionsBuilderExtensions
    {
        /// <summary>
        /// 將 schema 名稱注入 DbContextOptions，
        /// BaseDbContext 建構子透過 FindExtension 取回此值。
        /// </summary>
        public static DbContextOptionsBuilder UseCustomSchema(
            this DbContextOptionsBuilder builder,
            string schema)
        {
            // WithExtension 是 EF Core 提供的標準擴充機制
            // 每次呼叫會產生新的 options 實例（immutable pattern）
            ((IDbContextOptionsBuilderInfrastructure)builder)
                .AddOrUpdateExtension(new ToolkitOptionsExtension { Schema = schema });

            return builder;
        }
    }
}
