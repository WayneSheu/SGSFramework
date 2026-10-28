using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Scheduler.Options;

namespace SGSFramework.Scheduler.Maintenance;

/// <summary>單一清除對象的結果</summary>
/// <param name="Deleted">刪除筆數；null 表示未執行（停用或不可清除）</param>
/// <param name="Note">略過原因或錯誤說明</param>
public sealed record LogCleanupTargetResult(string Table, string Description, int RetentionDays, int? Deleted, string? Note);

public sealed record LogCleanupResult(int TotalDeleted, IReadOnlyList<LogCleanupTargetResult> Targets, DateTimeOffset StartedAt, DateTimeOffset CompletedAt);

/// <summary>
/// 日誌清除：依保留天數分批刪除過期的非 Ledger 紀錄（Outbox、排程執行紀錄、管理異動軌跡）。
/// 系統日誌 / 資安日誌 / AuditLogs 為僅附加 Ledger 資料表，資料庫禁止刪除，僅列入結果供檢視筆數。
/// </summary>
public interface ILogCleanupService
{
    Task<LogCleanupResult> CleanupAsync(CancellationToken cancellationToken = default);
}

public sealed class LogCleanupService(
    DbContext dbContext,
    IOptionsMonitor<SchedulerOptions> schedulerOptions,
    TimeProvider timeProvider,
    ILogger<LogCleanupService> logger) : ILogCleanupService
{
    /// <summary>管理異動軌跡屬稽核證據，啟用清除時的最短保留天數</summary>
    private const int MinimumAuditTrailRetentionDays = 365;

    /// <summary>清除對象：資料表名稱、刪除條件與時間欄位皆為常數（不接受外部輸入，避免 SQL Injection）</summary>
    private sealed record Target(string Table, string Description, string Condition, Func<LogCleanupOptions, int> Retention);

    private static readonly Target[] Targets =
    [
        new("[core].[OutboxMessages]", "已處理的 Outbox 訊息", "ProcessedOnUtc IS NOT NULL AND IsDead = 0 AND ProcessedOnUtc < @threshold", o => o.OutboxProcessedRetentionDays),
        new("[core].[OutboxMessages]", "Outbox 死信", "IsDead = 1 AND OccurredOnUtc < @threshold", o => o.OutboxDeadLetterRetentionDays),
        new("[org].[OutboxMessages]", "組織模組已處理的 Outbox 訊息", "ProcessedOnUtc IS NOT NULL AND IsDead = 0 AND ProcessedOnUtc < @threshold", o => o.OutboxProcessedRetentionDays),
        new("[org].[OutboxMessages]", "組織模組 Outbox 死信", "IsDead = 1 AND OccurredOnUtc < @threshold", o => o.OutboxDeadLetterRetentionDays),
        new("[core].[ScheduledJobRuns]", "排程執行紀錄", "StartedAt < @threshold", o => o.ScheduledJobRunRetentionDays),
        new("[core].[RoleAuditLogs]", "角色異動紀錄", "CreatedAt < @threshold", AuditTrail),
        new("[core].[SystemAccountAuditLogs]", "系統帳號異動紀錄", "CreatedAt < @threshold", AuditTrail),
        new("[core].[SystemSettingChangeLogs]", "系統配置異動紀錄", "CreatedAt < @threshold", AuditTrail),
        new("[core].[FunctionStatusLogs]", "功能狀態異動紀錄", "CreatedAt < @threshold", AuditTrail)
    ];

    /// <summary>僅附加 Ledger 資料表：資料庫層禁止 DELETE（防篡改），僅回報現有筆數</summary>
    private static readonly (string Table, string Description)[] LedgerTables =
    [
        ("[core].[SystemLogs]", "系統日誌"),
        ("[core].[SecurityLog]", "資安日誌"),
        ("[core].[AuditLogs]", "資料異動稽核"),
        ("[org].[AuditLogs]", "組織模組資料異動稽核")
    ];

    private static int AuditTrail(LogCleanupOptions o) =>
        o.AuditTrailRetentionDays <= 0 ? 0 : Math.Max(o.AuditTrailRetentionDays, MinimumAuditTrailRetentionDays);

    public async Task<LogCleanupResult> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var options = schedulerOptions.CurrentValue.LogCleanup;
        var startedAt = timeProvider.GetUtcNow();
        var results = new List<LogCleanupTargetResult>();

        foreach (var target in Targets)
        {
            var retention = target.Retention(options);
            if (retention <= 0)
            {
                results.Add(new(target.Table, target.Description, 0, null, "未啟用（保留天數為 0）"));
                continue;
            }

            try
            {
                var deleted = await DeleteInBatchesAsync(target, startedAt.AddDays(-retention), options.BatchSize, cancellationToken).ConfigureAwait(false);
                results.Add(new(target.Table, target.Description, retention, deleted, null));
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // 模組未安裝（例如無 org Schema）
                results.Add(new(target.Table, target.Description, retention, null, "資料表不存在，略過"));
            }
            catch (SqlException ex)
            {
                logger.LogWarning(ex, "[LogCleanup] 清除 {Table}（{Description}）失敗 (SQL 錯誤 {Number})。", target.Table, target.Description, ex.Number);
                results.Add(new(target.Table, target.Description, retention, null, $"清除失敗：{ex.Message}"));
            }
        }

        foreach (var (table, description) in LedgerTables)
        {
            var rows = await CountRowsAsync(table, cancellationToken).ConfigureAwait(false);
            results.Add(new(table, description, 0, null,
                rows is null ? "僅附加 Ledger 資料表，資料庫禁止刪除" : $"僅附加 Ledger 資料表，資料庫禁止刪除（目前 {rows:N0} 筆）"));
        }

        var failed = results.Count(r => r.Note?.StartsWith("清除失敗", StringComparison.Ordinal) == true);
        if (failed > 0 && failed == results.Count(r => r.RetentionDays > 0))
        {
            throw new LogCleanupException($"所有清除對象皆失敗：{string.Join("；", results.Where(r => r.Note is not null && r.RetentionDays > 0).Select(r => $"{r.Description} {r.Note}"))}");
        }

        var completedAt = timeProvider.GetUtcNow();
        var total = results.Sum(r => r.Deleted ?? 0);
        logger.LogInformation("[LogCleanup] 日誌清除完成，共刪除 {Total} 筆，耗時 {Elapsed}。{Details}",
            total, completedAt - startedAt,
            string.Join("；", results.Where(r => r.Deleted > 0).Select(r => $"{r.Description} {r.Deleted}")));

        return new LogCleanupResult(total, results, startedAt, completedAt);
    }

    private async Task<int> DeleteInBatchesAsync(Target target, DateTimeOffset threshold, int batchSize, CancellationToken cancellationToken)
    {
        // 時間欄位型別不同（datetime2 為 UTC、datetimeoffset），以 UTC DateTime 參數比較兩者皆正確
        var sql = $"DELETE TOP (@batch) FROM {target.Table} WHERE {target.Condition};";
        int total = 0, deleted;
        do
        {
            deleted = await dbContext.Database.ExecuteSqlRawAsync(sql,
                [new SqlParameter("@batch", batchSize), new SqlParameter("@threshold", threshold.UtcDateTime)],
                cancellationToken).ConfigureAwait(false);
            total += deleted;
        }
        while (deleted == batchSize && !cancellationToken.IsCancellationRequested);

        return total;
    }

    private async Task<long?> CountRowsAsync(string table, CancellationToken cancellationToken)
    {
        try
        {
            // sys.partitions 取近似筆數，避免對大型日誌表 COUNT(*) 全表掃描
            return await dbContext.Database.SqlQueryRaw<long>(
                    "SELECT CAST(SUM(rows) AS bigint) AS [Value] FROM sys.partitions WHERE object_id = OBJECT_ID({0}) AND index_id IN (0, 1)",
                    table)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException)
        {
            return null;
        }
    }
}

/// <summary>清除無法執行（全部對象失敗），屬可預期的業務失敗</summary>
public sealed class LogCleanupException(string message) : Exception(message);
