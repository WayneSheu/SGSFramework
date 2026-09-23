// ==========================================
// 檔案路徑: Host/PhysLIMS.API/PhysLIMSDbContextFactory.cs
// 架構層級: Presentation / Host Layer (EF Core Design-Time Factory)
// 說明: PhysLIMSDbContext 設計時期 Factory，符合 .NET 10 與 Clean Architecture 企業級規範
// ==========================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhysLIMS.API.Dbcontexts;
using SGSFramework.AuditLog.Abstractions;
using SGSFramework.AuditLog.Channels;
using SGSFramework.AuditLog.Interceptors;
using SGSFramework.Core.Migrations;
using System;
using System.IO;

namespace PhysLIMS.API
{
    /// <summary>
    /// 提供設計時期 <see cref="PhysLIMSDbContext"/> 工廠實作，供 EF Core CLI / PMC 執行 Migration 相關指令。
    /// </summary>
    public class PhysLIMSDbContextFactory : IDesignTimeDbContextFactory<PhysLIMSDbContext>
    {
        public PhysLIMSDbContext CreateDbContext(string[] args)
        {
            try
            {
                // 1. 安全解析 Assembly 與配置檔目錄路徑
                var assemblyLocation = typeof(PhysLIMSDbContext).Assembly.Location;
                var assemblyDirectory = string.IsNullOrWhiteSpace(assemblyLocation)
                    ? AppContext.BaseDirectory
                    : Path.GetDirectoryName(assemblyLocation) ?? AppContext.BaseDirectory;

                var configuration = new ConfigurationBuilder()
                    .SetBasePath(assemblyDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();

                // 2. 獲取 Migration 專用連線字串，提供開發預設容錯值
                var connectionString = configuration.GetSection("PersistentSettings:ConnectionStrings")["MigrationConnection"]
                    ?? configuration.GetSection("PersistentSettings:ConnectionStrings")["DefaultConnection"]
                    ?? configuration.GetConnectionString("DefaultConnection")
                    ?? "Server=localhost;Database=PhysLIMS_DB;Integrated Security=True;TrustServerCertificate=True;";

                // 3. 配置設計時期專用的 ServiceCollection，補足泛型 AuditChannel 與 Interceptors 依賴
                var services = new ServiceCollection();

                services.AddLogging(builder => builder.AddConsole());
                services.AddHttpContextAccessor();

                // 註冊 PhysLIMSDbContext 專屬的獨立泛型 Channel 佇列與 Interceptors
                services.AddSingleton<IAuditChannel<PhysLIMSDbContext>, AuditChannel<PhysLIMSDbContext>>();
                services.AddTransient<AuditInterceptor>();
                services.AddTransient<PermissionAuditInterceptor>();

                var serviceProvider = services.BuildServiceProvider();

                // 4. 解析 Interceptors 實例
                var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
                var permissionInterceptor = serviceProvider.GetService<PermissionAuditInterceptor>();

                // 5. 建立與配置 OptionsBuilder
                var optionsBuilder = new DbContextOptionsBuilder<PhysLIMSDbContext>();

                optionsBuilder.UseSqlServer(connectionString, sql =>
                {
                    var assemblyName = typeof(PhysLIMSDbContext).Assembly.FullName
                        ?? throw new InvalidOperationException("無法取得 PhysLIMSDbContext Assembly 的完整名稱。");

                    sql.MigrationsAssembly(assemblyName);
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "core");
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                });

                // 6. 掛載 ServiceProvider 與 Interceptors 至 DbContextOptionsBuilder
                optionsBuilder.UseApplicationServiceProvider(serviceProvider);

                if (auditInterceptor != null && permissionInterceptor != null)
                {
                    optionsBuilder.AddInterceptors(auditInterceptor, permissionInterceptor);
                }
                else if (auditInterceptor != null)
                {
                    optionsBuilder.AddInterceptors(auditInterceptor);
                }

                // 7. 替換客製化 EF Core Metadata 與 Migration SQL 生成服務
                optionsBuilder
                    .ReplaceService<IRelationalAnnotationProvider, CustomSqlServerAnnotationProvider>()
                    .ReplaceService<IMigrationsSqlGenerator, CustomSqlServerMigrationsSqlGenerator>();

                // 8. 傳入設定完成的 Options 實例化 DbContext
                return new PhysLIMSDbContext(optionsBuilder.Options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"建立 PhysLIMSDbContext 設計時期實體失敗: {ex.Message}", ex);
            }
        }
    }
}