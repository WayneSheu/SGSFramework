using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Migrations;

namespace SGS.Modules.ORG.Infrastructure
{
    /// <summary>
    /// Org 模組專屬的資料庫遷移服務，負責管理該模組的 Migration 檔案與資料庫套用狀態
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

        /// <summary>
        /// 取得該模組目前所有已在本機組件中定義的 Migration 識別名稱清單
        /// </summary>
        public IEnumerable<string> GetLocalMigrations()
        {
            try
            {
                var migrationsAssembly = _context.GetService<IMigrationsAssembly>();
                var localMigrations = migrationsAssembly.Migrations.Keys;
                return localMigrations ?? Enumerable.Empty<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ORG 模組] 取得本機 Migration 檔案清單時發生錯誤。");
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// 取得該模組已套用至資料庫的 Migration 識別名稱清單
        /// </summary>
        public async Task<IEnumerable<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ORG 模組] 取得已套用至資料庫的 Migration 清單時發生錯誤。");
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// 取得該模組尚未套用至資料庫的 Migration 識別名稱清單（待處理項目）
        /// </summary>
        public async Task<IEnumerable<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var connectionString = _context.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("[ORG 模組] 嚴重錯誤：DbContext 連接字串為空！請檢查依賴註冊。");
                    throw new InvalidOperationException("資料庫連線資訊遺失。");
                }

                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
                return pendingMigrations ?? Enumerable.Empty<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ORG 模組] 取得待處理的 Migration 清單時發生錯誤。");
                throw;
            }
        }

        /// <summary>
        /// 執行該模組的資料庫遷移
        /// </summary>
        public async Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(">>> [ORG 模組] 開始初始化模組資料庫結構與 Schema...");

                // 1. 保障 Schema 'org' 存在
                await _context.Database.ExecuteSqlRawAsync(
                    "IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'org') EXEC('CREATE SCHEMA org')",
                    cancellationToken).ConfigureAwait(false);

                // 2. 驗證 EF Core 是否正確識別 Assembly 內的 Migration 類別
                var localMigrations = GetLocalMigrations().ToList();
                _logger.LogInformation(">>> [ORG 模組 Migration 診斷] 本機組件內識別到 {Count} 個 Migration 檔案。", localMigrations.Count);

                if (!localMigrations.Any())
                {
                    _logger.LogWarning(">>> [警告] EF Core IMigrationsAssembly 未掃描到任何 Migration！請確認 DbContextOptions 配置是否包含 .MigrationsAssembly()。");
                }

                // 3. 檢查待處理的 Migration
                var pendingMigrations = (await GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
                var appliedMigrations = (await GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

                _logger.LogInformation(">>> [ORG 模組 Migration 狀態] 已套用: {AppliedCount} 筆, 待套用: {PendingCount} 筆",
                    appliedMigrations.Count, pendingMigrations.Count);

                if (pendingMigrations.Any())
                {
                    _logger.LogInformation(">>> [ORG 模組] 發現 {Count} 個待處理遷移，開始執行升級套用...", pendingMigrations.Count);
                    foreach (var migrationName in pendingMigrations)
                    {
                        _logger.LogInformation(">>> [準備套用 Migration]: {MigrationId}", migrationName);
                    }

                    // 4. 執行真正的建表與資料庫異動
                    await _context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
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

        /// <summary>
        /// 執行診斷檢查，確認模組的遷移檔案與資料庫狀態是否一致，並列出任何不一致的項目
        /// </summary>
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

                // 比對不一致狀態（例如 DB 有但本機找不到）
                var missingInLocal = appliedMigrations.Except(localMigrations).ToList();
                if (missingInLocal.Any())
                {
                    foreach (var missing in missingInLocal)
                    {
                        _logger.LogError("   - [不一致] 資料庫已記錄但本機 Assembly 遺失: {MigrationId}", missing);
                    }
                }

                _logger.LogInformation("==================================================");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ORG 模組] 執行 DiagnosticMigrations 診斷時發生例外異常。");
            }
        }
    }
}