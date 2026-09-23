// ==========================================
// 檔案路徑: Infrastructure/SGS.Modules.ORG.Infrastructure/Dbcontexts/ORGDbContextFactory.cs
// 架構層級: Infrastructure Layer (EF Core Design Time Factory)
// ==========================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGSFramework.AuditLog.Abstractions;
using SGSFramework.AuditLog.Channels;
using SGSFramework.AuditLog.Interceptors;
using System;
using System.IO;

namespace SGS.Modules.ORG.Infrastructure.Dbcontexts
{
    /// <summary>
    /// 提供設計時期 DbContext 工廠，供 EF Core CLI / Package Manager Console 執行 `add-migration` 與 `database update` 使用。
    /// </summary>
    public class ORGDbContextFactory : IDesignTimeDbContextFactory<ORGDbContext>
    {
        public ORGDbContext CreateDbContext(string[] args)
        {
            // 1. 安全解析 Assembly 與配置檔目錄路徑
            var assemblyLocation = typeof(ORGDbContext).Assembly.Location;
            var assemblyDirectory = string.IsNullOrWhiteSpace(assemblyLocation)
                ? AppContext.BaseDirectory
                : Path.GetDirectoryName(assemblyLocation) ?? AppContext.BaseDirectory;

            var configuration = new ConfigurationBuilder()
                .SetBasePath(assemblyDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<ORGDbContext>();

            // 優先嘗試從配置檔獲取連線字串，否則使用預設開發連線字串
            var connectionString = configuration.GetSection("PersistentSettings:ConnectionStrings")["MigrationConnection"]
                ?? configuration.GetSection("PersistentSettings:ConnectionStrings")["DefaultConnection"]
                ?? "Server=localhost;Database=PhysLIMS_DB;Integrated Security=True;TrustServerCertificate=True;";

            optionsBuilder.UseSqlServer(connectionString, sql =>
            {
                var assemblyName = typeof(ORGDbContext).Assembly.FullName
                    ?? throw new InvalidOperationException("無法取得 ORGDbContext Assembly 的完整名稱。");

                sql.MigrationsAssembly(assemblyName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "org");
            });

            // 2. 配置設計時期專用的 ServiceCollection，補足泛型 AuditChannel 與 AuditInterceptor 依賴
            var services = new ServiceCollection();

            services.AddLogging(builder => builder.AddConsole());
            services.AddHttpContextAccessor();

            // 註冊特定 Context (ORGDbContext) 專屬的獨立泛型 Channel 佇列與 Interceptors
            services.AddSingleton<IAuditChannel<ORGDbContext>, AuditChannel<ORGDbContext>>();
            services.AddTransient<AuditInterceptor>();
            services.AddTransient<PermissionAuditInterceptor>();

            var serviceProvider = services.BuildServiceProvider();

            // 3. 解析 AuditInterceptor 並掛載至 DbContextOptionsBuilder
            var interceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
            var permissionInterceptor = serviceProvider.GetService<PermissionAuditInterceptor>();

            if (interceptor != null && permissionInterceptor != null)
            {
                optionsBuilder.AddInterceptors(interceptor, permissionInterceptor);
            }
            else if (interceptor != null)
            {
                optionsBuilder.AddInterceptors(interceptor);
            }

            // 4. 傳入建構子參數並回傳實體
            return new ORGDbContext(optionsBuilder.Options);
        }
    }
}