using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Migrations;
using System.Reflection;

namespace SGS.Modules.ORG.Infrastructure.Services;

/// <summary>
/// Org 模組專屬的資料庫遷移服務，採用穩健的手動 DDL 執行模式支援動態 ALC 環境
/// </summary>
public sealed class OrgMigrationService : IMigrationService
{
    private readonly ORGDbContext _context;
    private readonly ILogger<OrgMigrationService> _logger;

    public OrgMigrationService(ORGDbContext context, ILogger<OrgMigrationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IEnumerable<string> GetLocalMigrations()
    {
        try
        {
            var assembly = typeof(ORGDbContext).Assembly;
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
            _logger.LogError(ex, "[ORG 模組] 取得本機 Migration 檔案清單時發生錯誤。");
            return Enumerable.Empty<string>();
        }
    }

    public async Task<IEnumerable<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 確保歷史紀錄表存在
            await EnsureHistoryTableExistsAsync(cancellationToken).ConfigureAwait(false);

            // 查詢已套用的 Migration
            return await _context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ORG 模組] 取得已套用至資料庫的 Migration 清單時發生錯誤。");
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
            _logger.LogError(ex, "[ORG 模組] 取得待處理的 Migration 清單時發生錯誤。");
            throw;
        }
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(">>> [ORG 模組] 開始初始化模組資料庫結構與 Schema...");

            // 1. 保障 Schema 'org' 存在
            await _context.Database.ExecuteSqlRawAsync(
                "IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'org') EXEC('CREATE SCHEMA org')",
                cancellationToken).ConfigureAwait(false);

            // 2. 保障 __EFMigrationsHistory 存在
            await EnsureHistoryTableExistsAsync(cancellationToken).ConfigureAwait(false);

            var pendingMigrations = (await GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

            if (pendingMigrations.Any())
            {
                _logger.LogInformation(">>> [ORG 模組] 發現 {Count} 個待處理遷移，開始執行升級套用...", pendingMigrations.Count);

                var assembly = typeof(ORGDbContext).Assembly;
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

                _logger.LogInformation(">>> [ORG 模組] 資料庫結構套用成功。");
            }
            else
            {
                _logger.LogInformation(">>> [ORG 模組] 資料庫結構已是最新的，無需重複套用。");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ORG 模組] 資料庫遷移發生嚴重錯誤。");
            throw;
        }
    }

    private async Task EnsureHistoryTableExistsAsync(CancellationToken cancellationToken)
    {
        var createTableSql = @"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[org].[__EFMigrationsHistory]') AND type in (N'U'))
BEGIN
    CREATE TABLE [org].[__EFMigrationsHistory] (
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
                    await _context.Database.ExecuteSqlRawAsync(command.CommandText, cancellationToken).ConfigureAwait(false);
                }

                // 寫入 Migration 歷史紀錄
                var productVersion = typeof(DbContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "10.0.0";
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO [org].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ({0}, {1})",
                    new object[] { migrationId, productVersion },
                    cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(">>> [ORG 模組] Migration {MigrationId} 腳本執行並套用成功。", migrationId);
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

            _logger.LogInformation("========== [ORG 模組 Migration 診斷報告] ==========");
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
            _logger.LogError(ex, "[ORG 模組] 執行 DiagnosticMigrations 診斷時發生例外異常。");
        }
    }
}