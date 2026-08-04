using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Migrations;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using System.Reflection;

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

        public IEnumerable<string> GetLocalMigrations()
        {
            try
            {
                // 取得該模組 DbContext 內目前編譯的所有實體 Migration 檔案名稱
                var migrations = _context.Database.GetMigrations();
                return migrations ?? Enumerable.Empty<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得 Org 模組本機 Migration 檔案清單時發生錯誤。");
                return Enumerable.Empty<string>();
            }
        }

        public async Task<IEnumerable<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // 查詢資料庫 __EFMigrationsHistory 資料表中已套用的 Migration 紀錄
                return await _context.Database.GetAppliedMigrationsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得 Org 模組已套用至資料庫的 Migration 清單時發生錯誤。");
                return Enumerable.Empty<string>();
            }
        }

        public async Task<IEnumerable<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var connectionString = _context.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("嚴重錯誤：DbContext 連接字串為空！請檢查依賴註冊。");
                    throw new InvalidOperationException("資料庫連線資訊遺失。");
                }

                // 重新確認模型是否已載入，避免因動態載入導致模型資訊遺失
                _context.Model.GetEntityTypes();
                // 比較本機程式碼與資料庫紀錄，計算出尚未套用的 Migration 檔案
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
                // 確保回傳值永遠不為 null，避免後續邏輯崩潰
                return pendingMigrations ?? Enumerable.Empty<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得 Org 模組待處理的 Migration 清單時發生錯誤。");
                //return Enumerable.Empty<string>();
                throw; // 拋出異常，不要靜默失敗
            }
        }

        public async Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. 強制設定歷史表所在位置 (這裡必須與 OnConfiguring 的設定完全一致)
                // 雖然 ORGDbContext 有設定，但在動態載入環境，建議在遷移前再次確認
                //_context.Database.SetCommandTimeout(120);
                // 2. 這行代碼若失敗，代表 ORGDbContext 的配置未生效
                //await _context.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("開始套用模組資料庫遷移...");
                // 確保 Schema 存在，這是執行遷移前的必要保障
                await _context.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'org') EXEC('CREATE SCHEMA org')");
                // 強制觸發模型建立，確保動態載入的類型已被加入到模型中
                //_context.Model.GetEntityTypes();

                //驗證遷移檔案是否正確被掃描到
                //VerifyMigrations();
                // 2. 核心遷移邏輯，移除靜默失敗，加入明確的錯誤診斷
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
                if (pendingMigrations.Any())
                {
                    _logger.LogInformation(">>> [遷移] 發現 {Count} 個待處理遷移...", pendingMigrations.Count());
                    await _context.Database.MigrateAsync(cancellationToken);
                }
                else
                {
                    _logger.LogInformation(">>> [遷移] 資料庫結構已是最新的。");
                }


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "模組資料庫遷移發生錯誤。");
                throw;// 生產環境應拋出異常以阻止錯誤的系統狀態啟動
            }
        }

        /// <summary>
        /// 診斷模組的 Migration 狀態，列出所有已編譯的遷移檔案名稱
        /// </summary>
        public async Task DiagnosticMigrations()
        {
            // 取得當前 DbContext 關聯的所有遷移資訊
            var migrations = GetLocalMigrationsAlternative();//_context.Database.GetMigrations();

            _logger.LogInformation(">>> [診斷] 掃描到總遷移數: {Count}", migrations.Count());

            foreach (var migration in migrations)
            {
                _logger.LogInformation(">>> [診斷] 發現遷移檔案: {Name}", migration);
            }
        }

        public IEnumerable<string> GetLocalMigrationsAlternative()
        {
            // 直接透過 typeof(ORGDbContext) 取得該模組的 Assembly
            var assembly = typeof(ORGDbContext).Assembly;

            // 使用上述的手動反射邏輯
            var migrations = GetMigrationsManually(assembly);

            _logger.LogInformation(">>> [手動掃描] 成功識別到 {Count} 個遷移檔案", migrations.Count());
            return migrations;
        }

        /// <summary>
        /// 手動掃描指定 Assembly 中的 Migration 類別，並返回其名稱列表
        /// </summary>
        /// <param name="migrationAssembly"></param>
        /// <returns></returns>
        public IEnumerable<string> GetMigrationsManually(Assembly migrationAssembly)
        {
            // 1. 取得 Assembly 中所有繼承自 Migration 的類別
            var migrationTypes = migrationAssembly.GetTypes()
                .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            // 2. 轉換為名稱 (EF Core 的遷移名稱格式: {Timestamp}_{Name})
            return migrationTypes
                .Select(t => t.Name)
                .OrderBy(name => name);
        }


        public void VerifyMigrations()
        {
            var assembly = typeof(ORGDbContext).Assembly;
            var migrations = _context.Database.GetMigrations();

            _logger.LogInformation(">>> 診斷：DbContext Assembly 為 {Name}", assembly.FullName);
            _logger.LogInformation(">>> 診斷：掃描到遷移數量: {Count}", migrations.Count());

            if (!migrations.Any())
            {
                _logger.LogWarning(">>> 診斷：EF Core 未能掃描到任何 Migration 檔案，請檢查 MigrationsAssembly 配置是否指向了正確的 Assembly。");
            }
        }

    }
}
