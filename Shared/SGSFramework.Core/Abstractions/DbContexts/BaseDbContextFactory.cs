using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.DbContexts
{
    /// <summary>
    /// 提供設計時期 DbContext 工廠的基底類別，方便在執行 `dotnet ef migrations` 時使用。
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public abstract class BaseDbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext>
        where TContext : DbContext
    {
        // 強制子類別提供專案名稱，用以指定 Migration Assembly
        protected abstract string MigrationAssemblyName { get; }

        public TContext CreateDbContext(string[] args)
        {
            var apiProjectPath = GetApiProjectPath();
            var configuration = BuildConfiguration(apiProjectPath);
            var optionsBuilder = new DbContextOptionsBuilder<TContext>();

            // 讀取設定 (您可以根據專案結構調整路徑，或透過建構函式傳入 Key)
            var connectionString = configuration.GetSection("PersistentOptions:DatabaseSettings:ConnectionString").Value;

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("無法取得連線字串。");

            optionsBuilder.UseSqlServer(connectionString, sql => {
                sql.MigrationsAssembly(MigrationAssemblyName);
            });

            return CreateNewInstance(optionsBuilder.Options);
        }

        // 抽象方法：由子類別決定如何實例化 TContext
        protected abstract TContext CreateNewInstance(DbContextOptions<TContext> options);

        private string GetApiProjectPath()
        {
            string? path = Environment.GetEnvironmentVariable("SES_API_PATH");
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), "../../../../API/SES.API");

            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"找不到 API 專案目錄: {Path.GetFullPath(path)}");

            return path;
        }

        private IConfiguration BuildConfiguration(string basePath)
        {
            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }
    }
}
