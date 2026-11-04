using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGS.Modules.FMS.Infrastructure.Dbcontexts;
using SGSFramework.Core.Migrations;
using System.Reflection;

namespace SGS.Modules.FMS.Infrastructure.Services;

/// <summary>
/// FMS 模組專屬的資料庫遷移服務，採用穩健的手動 DDL 執行模式支援動態 ALC 環境
/// </summary>
public sealed class FmsMigrationService : IMigrationService
{
    private readonly FMSDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FmsMigrationService> _logger;

    public FmsMigrationService(FMSDbContext context, IConfiguration configuration, ILogger<FmsMigrationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IEnumerable<string> GetLocalMigrations()
    {
        try
        {
            var assembly = typeof(FMSDbContext).Assembly;
            var localMigrations = assembly.GetTypes()
                .Where(t => typeof(Migration).IsAssignableFrom(t)
                         && t.GetCustomAttributes(typeof(MigrationAttribute), false).Length > 0)
                .Select(t => t.GetCustomAttribute<MigrationAttribute>()!.Id)
                .OrderBy(id => id)
                .ToList();

            return localMigrations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FMS 模組] 取得本機 Migration 檔案清單時發生錯誤。");
            return Enumerable.Empty<string>();
        }
    }

    public async Task<IEnumerable<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 建立歷史紀錄表屬 DDL：以部署帳號執行 (最小權限，執行期帳號無 CREATE TABLE)
            using var _ = _context.Database.UseMigrationConnection(_configuration);

            // 確保歷史紀錄表存在
            await EnsureHistoryTableExistsAsync(cancellationToken).ConfigureAwait(false);

            // 查詢已套用的 Migration
            return await _context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FMS 模組] 取得已套用至資料庫的 Migration 清單時發生錯誤。");
            return Enumerable.Empty<string>();
        }
    }

    public async Task<IEnumerable<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var localMigrations = GetLocalMigrations();
            var appliedMigrations = await GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);

            return localMigrations.Except(appliedMigrations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FMS 模組] 取得待處理的 Migration 清單時發生錯誤。");
            throw;
        }
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(">>> [FMS 模組] 開始初始化模組資料庫結構與 Schema...");

            // 建立 Schema / 資料表屬 DDL：僅此範圍改用部署帳號 (MigrationConnection)，結束即切回執行期帳號
            using var _ = _context.Database.UseMigrationConnection(_configuration);

            // 1. 保障 Schema 'fms' 存在
            await _context.Database.ExecuteSqlRawAsync(
                "IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'fms') EXEC('CREATE SCHEMA fms')",
                cancellationToken).ConfigureAwait(false);

            // 1-1. 執行期帳號 (DefaultConnection) 需 fms 的 DML 權限（檔案中繼資料、版本、存取紀錄）
            await GrantRuntimeAccessAsync(cancellationToken).ConfigureAwait(false);

            // 2. 保障 __EFMigrationsHistory 存在
            await EnsureHistoryTableExistsAsync(cancellationToken).ConfigureAwait(false);

            var pendingMigrations = (await GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

            if (pendingMigrations.Any())
            {
                _logger.LogInformation(">>> [FMS 模組] 發現 {Count} 個待處理遷移，開始執行升級套用...", pendingMigrations.Count);

                var assembly = typeof(FMSDbContext).Assembly;
                var migrationTypes = assembly.GetTypes()
                    .Where(t => typeof(Migration).IsAssignableFrom(t)
                             && t.GetCustomAttributes(typeof(MigrationAttribute), false).Length > 0)
                    .ToDictionary(t => t.GetCustomAttribute<MigrationAttribute>()!.Id, t => t);

                foreach (var migrationId in pendingMigrations)
                {
                    if (migrationTypes.TryGetValue(migrationId, out var migrationType))
                    {
                        _logger.LogInformation(">>> [準備套用 Migration]: {MigrationId}", migrationId);

                        // 嘗試標準套用，若無異動則進行腳本顯式構建執行
                        await ExecuteMigrationTypeAsync(migrationType, migrationId, cancellationToken).ConfigureAwait(false);
                    }
                }

                _logger.LogInformation(">>> [FMS 模組] 資料庫結構套用成功。");
            }
            else
            {
                _logger.LogInformation(">>> [FMS 模組] 資料庫結構已是最新的，無需重複套用。");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FMS 模組] 資料庫遷移發生嚴重錯誤。");
            throw;
        }
    }

    /// <summary>
    /// 授予執行期帳號 fms schema 的 DML 權限；部署帳號無權授權時僅記錄警告，改由 DBA 執行 docs/sql/20261104_grant_fms_schema.sql
    /// </summary>
    private async Task GrantRuntimeAccessAsync(CancellationToken cancellationToken)
    {
        var defaultConnection = _configuration["PersistentSettings:ConnectionStrings:DefaultConnection"];
        if (string.IsNullOrWhiteSpace(defaultConnection)) return;

        string? runtimeUser;
        try
        {
            runtimeUser = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(defaultConnection).UserID;
        }
        catch (ArgumentException)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(runtimeUser)) return;

        try
        {
            // 帳號名稱以參數傳入，並於 T-SQL 內以 QUOTENAME 組合，避免注入
            await _context.Database.ExecuteSqlRawAsync(
                """
                DECLARE @user sysname = {0};
                IF DATABASE_PRINCIPAL_ID(@user) IS NOT NULL
                BEGIN
                    DECLARE @sql nvarchar(400) = N'GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[fms] TO ' + QUOTENAME(@user) + N';';
                    EXEC (@sql);
                END
                """,
                [runtimeUser],
                cancellationToken).ConfigureAwait(false);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            _logger.LogWarning(ex, "[FMS 模組] 無法授予執行期帳號 fms schema 權限，請由 DBA 執行 docs/sql/20261104_grant_fms_schema.sql。");
        }
    }

    private async Task EnsureHistoryTableExistsAsync(CancellationToken cancellationToken)
    {
        var createTableSql = @"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[fms].[__EFMigrationsHistory]') AND type in (N'U'))
BEGIN
    CREATE TABLE [fms].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;";
        await _context.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteMigrationTypeAsync(Type migrationType, string migrationId, CancellationToken cancellationToken)
    {
        // 透過 EF Core 內部的 Service 產生特定 Migration 的原生 SQL 腳本
        var migrator = _context.GetService<IMigrationsSqlGenerator>();
        var migration = (Migration)Activator.CreateInstance(migrationType)!;

        var builder = new MigrationBuilder(_context.Database.ProviderName);

        // 觸發 Up 方法填寫 MigrationBuilder 命令
        var upMethod = migrationType.GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        upMethod?.Invoke(migration, new object[] { builder });

        if (builder.Operations.Any())
        {
            var commands = migrator.Generate(builder.Operations, _context.Model);

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var command in commands)
                {
                    // ExecuteSqlRaw 會以 string.Format 處理 SQL，欄位說明等文字中的大括號須跳脫，否則拋出 FormatException
                    await _context.Database.ExecuteSqlRawAsync(command.CommandText.Replace("{", "{{").Replace("}", "}}"), cancellationToken).ConfigureAwait(false);
                }

                // 寫入 Migration 歷史紀錄
                var productVersion = typeof(DbContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "10.0.0";
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO [fms].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ({0}, {1})",
                    new object[] { migrationId, productVersion },
                    cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(">>> [FMS 模組] Migration {MigrationId} 腳本執行並套用成功。", migrationId);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        else
        {
            // 若 Operations 為空，嘗試使用預設 MigrateAsync 作為備用機制
            await _context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DiagnosticMigrations()
    {
        try
        {
            var localMigrations = GetLocalMigrations().ToList();
            var appliedMigrations = (await GetAppliedMigrationsAsync().ConfigureAwait(false)).ToList();
            var pendingMigrations = (await GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();

            _logger.LogInformation("========== [FMS 模組 Migration 診斷報告] ==========");
            _logger.LogInformation("1. 本機組件已編譯 Migration 總數: {Count}", localMigrations.Count);
            foreach (var m in localMigrations)
            {
                _logger.LogInformation("   - [Local] {MigrationId}", m);
            }

            _logger.LogInformation("2. 資料庫歷史紀錄 (__EFMigrationsHistory) 已套用總數: {Count}", appliedMigrations.Count);
            foreach (var m in appliedMigrations)
            {
                _logger.LogInformation("   - [Applied] {MigrationId}", m);
            }

            _logger.LogInformation("3. 尚未套用 (Pending) 總數: {Count}", pendingMigrations.Count);
            foreach (var m in pendingMigrations)
            {
                _logger.LogWarning("   - [Pending] {MigrationId}", m);
            }

            _logger.LogInformation("==================================================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FMS 模組] 執行 DiagnosticMigrations 診斷時發生例外異常。");
        }
    }
}