using Microsoft.Extensions.Configuration;

namespace SGS.Modules.LOG.Infrastructure.Extensions;

/// <summary>獨立日誌資料庫的設定鍵（與主庫 DefaultConnection / MigrationConnection 分離）。</summary>
public static class LogDatabaseOptions
{
    public const string Section = "PersistentSettings:ConnectionStrings";

    /// <summary>執行期帳號（僅 INSERT / SELECT）。</summary>
    public const string RuntimeConnectionKey = Section + ":LogConnection";

    /// <summary>部署帳號（DDL：建 schema、資料表、帳本）。</summary>
    public const string MigrationConnectionKey = Section + ":LogMigrationConnection";

    public static bool IsConfigured(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration[RuntimeConnectionKey]);
}
