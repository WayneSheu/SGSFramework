using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using SGSFramework.VerifyLedger.Dtos;
using System.Data;
using System.Data.Common;

namespace SGSFramework.VerifyLedger.Services
{
    public class MssqlLedgerVerificationService<TContext, TEntity> : ILedgerVerificationService<TContext, TEntity>
        where TContext : DbContext
        where TEntity : class, ILedgerEntity
    {
        private readonly TContext _context;
        private readonly ILogger<MssqlLedgerVerificationService<TContext, TEntity>> _logger;

        public MssqlLedgerVerificationService(TContext context, ILogger<MssqlLedgerVerificationService<TContext, TEntity>> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<LedgerVerificationResult> VerifyLedgerAsync(string? ledgerDigestJson = null)
        {
            var entityName = typeof(TEntity).Name;
            var contextName = typeof(TContext).Name;
            _logger.LogInformation("開始執行目標資料庫 {ContextName} 中實體 {EntityName} 的 MSSQL Ledger 完整性驗證。", contextName, entityName);

            try
            {
                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                // 核心整合：在執行任何 Ledger 操作前，動態檢查並修正快照隔離集 (Snapshot Isolation) 狀態
                await EnsureSnapshotIsolationEnabledAsync(connection);

                // 修正關鍵點：MSSQL 內建生成最新摘要的物件為系統儲存程序 'sys.sp_generate_database_ledger_digest'，非資料表檢視表
                if (string.IsNullOrWhiteSpace(ledgerDigestJson))
                {
                    _logger.LogWarning("未提供外部摘要公報 JSON，自動調用系統預設預存程序生成最新總帳快照。");

                    using var generateCommand = connection.CreateCommand();
                    generateCommand.CommandText = "EXEC sys.sp_generate_database_ledger_digest;";

                    if (generateCommand is DbCommand dbGenerateCommand)
                    {
                        using var digestReader = await dbGenerateCommand.ExecuteReaderAsync();
                        if (await digestReader.ReadAsync())
                        {
                            ledgerDigestJson = digestReader.GetValue(0)?.ToString();
                        }
                    }
                    else
                    {
                        using var digestReader = generateCommand.ExecuteReader();
                        if (digestReader.Read())
                        {
                            ledgerDigestJson = digestReader.GetValue(0)?.ToString();
                        }
                    }

                    if (string.IsNullOrWhiteSpace(ledgerDigestJson))
                    {
                        return LedgerVerificationResult.Failure("無法自 MSSQL 引擎生成有效的動態總帳摘要，拒絕調用驗證程序。", null);
                    }
                }

                using var command = connection.CreateCommand();

                // 加上 SET 宣告（當 DB 已啟用 ALLOW_SNAPSHOT_ISOLATION 時即可正常運作）
                string verifyCommandText = @"
                                            SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
                                            EXEC sys.sp_verify_database_ledger @digests;";

                command.CommandText = verifyCommandText;


                var digestsParam = new SqlParameter("@digests", SqlDbType.NVarChar, -1)
                {
                    Value = ledgerDigestJson
                };
                command.Parameters.Add(digestsParam);

                if (command is DbCommand dbVerifyCommand)
                {
                    using var reader = await dbVerifyCommand.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var lastVerificationResult = reader.GetValue(0)?.ToString() ?? "No output message.";
                        _logger.LogInformation("資料庫 {ContextName} 總帳驗證完成。回應: {Message}", contextName, lastVerificationResult);

                        return LedgerVerificationResult.Success($"來自 {contextName} 的驗證成功回應: {lastVerificationResult}", ledgerDigestJson);
                    }
                }
                else
                {
                    using var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        var lastVerificationResult = reader.GetValue(0)?.ToString() ?? "No output message.";
                        _logger.LogInformation("資料庫 {ContextName} 總帳驗證完成。回應: {Message}", contextName, lastVerificationResult);

                        return LedgerVerificationResult.Success($"來自 {contextName} 的驗證成功回應: {lastVerificationResult}", ledgerDigestJson);
                    }
                }

                return LedgerVerificationResult.Success($"資料庫 {contextName} 驗證命令執行完畢，鏈結結構未發現異常。", ledgerDigestJson);
            }
            catch (SqlException ex) when (ex.Number == 37346 || ex.Number == 37300)
            {
                _logger.LogCritical(ex, "【核心安全警報】資料庫 {ContextName} 的實體 {EntityName} 偵測到資料外力竄改或雜湊鏈結斷裂！", contextName, entityName);
                return LedgerVerificationResult.Failure($"總帳驗證失敗！偵測到資安惡意竄改事件。錯誤訊息: {ex.Message}", ledgerDigestJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行資料庫 {ContextName} 的實體 {EntityName} 總帳驗證時發生未預期的系統核心異常。", contextName, entityName);
                throw;
            }
        }

        /// <summary>
        /// 環境動態檢測與自我修復：確保當前連線資料庫已開啟快照集隔離等級
        /// </summary>
        private async Task EnsureSnapshotIsolationEnabledAsync(IDbConnection connection)
        {
            try
            {
                var targetDatabase = connection.Database;
                if (string.IsNullOrWhiteSpace(targetDatabase)) return;

                // 建立檢測與變更 SQL 命令
                // 注意：ALTER DATABASE 不允許在包含使用者事務（Transaction）的環境中直接執行，因此必須獨立處理
                string checkAndRepairSql = $"""
                    IF (SELECT snapshot_isolation_state FROM sys.databases WHERE name = '{targetDatabase}') = 0
                    BEGIN
                        EXEC('ALTER DATABASE [{targetDatabase}] SET ALLOW_SNAPSHOT_ISOLATION ON');
                    END
                    """;

                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = checkAndRepairSql;
                checkCommand.CommandType = CommandType.Text;

                _logger.LogInformation("正在檢測目標資料庫 [{Database}] 的快照集隔離狀態 (Snapshot Isolation Level)...", targetDatabase);

                if (checkCommand is DbCommand dbCommand)
                {
                    await dbCommand.ExecuteNonQueryAsync();
                }
                else
                {
                    checkCommand.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                // 進行權限不足時的優雅降級防護：如果執行端連線帳號非 DB_Owner 或 SA，將無法修改 DDL 屬性。
                // 此處記錄 Warning 警報而非阻斷，以防干擾已有 DBA 預先配置完成的環境。
                _logger.LogWarning(ex, "環境自我修復程序跳過：嘗試自動變更 ALLOW_SNAPSHOT_ISOLATION 時失敗。請確認執行帳號是否具備 ALTER DATABASE 權限，或請 DBA 手動開啟。");
            }
        }
    }
}