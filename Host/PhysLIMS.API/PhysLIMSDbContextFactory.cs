using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata; // 必須引入以使用 IRelationalAnnotationProvider
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhysLIMS.API.Dbcontexts;
using SGSFramework.AuditLog.Channels;
using SGSFramework.AuditLog.Interceptors;
using SGSFramework.Core.Migrations;
using System;
using System.IO;

namespace PhysLIMS.API
{
    public class PhysLIMSDbContextFactory : IDesignTimeDbContextFactory<PhysLIMSDbContext>
    {
        public PhysLIMSDbContext CreateDbContext(string[] args)
        {
            try
            {
                var assemblyLocation = typeof(PhysLIMSDbContext).Assembly.Location;
                var assemblyDirectory = string.IsNullOrWhiteSpace(assemblyLocation)
                    ? AppContext.BaseDirectory
                    : Path.GetDirectoryName(assemblyLocation) ?? AppContext.BaseDirectory;

                var configuration = new ConfigurationBuilder()
                    .SetBasePath(assemblyDirectory)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Development.json", optional: true)
                    .Build();

                var connectionString = configuration.GetSection("PersistentSettings:ConnectionStrings")["MigrationConnection"]
                    ?? configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("未於配置檔中找到有效的資料庫連線字串。");

                // 1. 優先建立設計時期的 ServiceProvider
                var services = new ServiceCollection();
                services.AddLogging(builder => builder.AddConsole());
                services.AddHttpContextAccessor();

                services.AddSingleton<AuditChannel>();
                services.AddTransient<AuditInterceptor>();

                var serviceProvider = services.BuildServiceProvider();
                var interceptor = serviceProvider.GetRequiredService<AuditInterceptor>();

                // 2. 建立與配置 OptionsBuilder
                var optionsBuilder = new DbContextOptionsBuilder<PhysLIMSDbContext>();

                optionsBuilder.UseSqlServer(connectionString, sql =>
                {
                    var assemblyName = typeof(PhysLIMSDbContext).Assembly.FullName
                        ?? throw new InvalidOperationException("無法取得 PhysLIMSDbContext Assembly 完整名稱。");

                    sql.MigrationsAssembly(assemblyName);
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "core");
                });

                // 3. 解決 CS1729：透過 EF Core 原生方法掛載 Interceptor 與外部服務，而非透過建構函式
                optionsBuilder.UseApplicationServiceProvider(serviceProvider);
                optionsBuilder.AddInterceptors(interceptor);

                // 4. 解決 CS0311：使用 IRelationalAnnotationProvider 替換舊版介面
                optionsBuilder
                    .ReplaceService<IRelationalAnnotationProvider, CustomSqlServerAnnotationProvider>()
                    .ReplaceService<IMigrationsSqlGenerator, CustomSqlServerMigrationsSqlGenerator>();

                // 5. 僅傳入單一 Options 參數實例化 DbContext
                return new PhysLIMSDbContext(optionsBuilder.Options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"建立 PhysLIMSDbContext 設計時期實體失敗: {ex.Message}", ex);
            }
        }
    }
}