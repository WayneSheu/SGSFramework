using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SGS.Modules.LOG.Infrastructure.Extensions;

namespace SGS.Modules.LOG.Infrastructure.Dbcontexts;

/// <summary>設計時期工廠，供 dotnet ef migrations add 使用（日誌庫不掛稽核攔截器）。</summary>
public class LogDbContextFactory : IDesignTimeDbContextFactory<LogDbContext>
{
    public LogDbContext CreateDbContext(string[] args)
    {
        var assemblyLocation = typeof(LogDbContext).Assembly.Location;
        var assemblyDirectory = string.IsNullOrWhiteSpace(assemblyLocation)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(assemblyLocation) ?? AppContext.BaseDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(assemblyDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile("claudesettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // add-migration 不會連線；未設定時使用本機預設值僅供產生模型
        var connectionString = new[] { configuration[LogDatabaseOptions.MigrationConnectionKey], configuration[LogDatabaseOptions.RuntimeConnectionKey] }
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
            ?? "Server=localhost;Database=PhysLIMSLogDB;Integrated Security=True;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<LogDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(LogDbContext).Assembly.FullName
                ?? throw new InvalidOperationException("無法取得 LogDbContext Assembly 的完整名稱。"));
            sql.MigrationsHistoryTable("__EFMigrationsHistory", "log");
        });

        return new LogDbContext(optionsBuilder.Options);
    }
}
