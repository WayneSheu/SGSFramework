# SGSFramework.Scheduler

以 Hangfire (SQL Server Storage) 實作的排程框架，整合系統帳號 (M2M) 執行週期作業。

## 架構

```
Hangfire 週期作業 (IRecurringJob)
   └─ SchedulerApiClient ── 系統帳號 Client Credentials 權杖 (Enterprise.HttpSdk)
         └─ 內部 API [RequiresSystemScope("...")]  ← 授權、稽核 (CreatedBy = sys_xxx)、撤銷檢查
```

- 作業只負責「何時」呼叫，「做什麼」留在 API 端；停用 / 凍結系統帳號即可中止排程的實際執行。
- `Scheduler:Enabled = false` 時不啟動 Hangfire，但內部端點仍可由 K8s CronJob 等外部排程器以系統帳號呼叫。
- 多節點：Hangfire 以資料庫分散式鎖確保週期作業只排入一次；`ServerEnabled` 可只在專用 Worker 開啟。
- IIS 託管時請將應用程式集區設為 AlwaysRunning 並啟用預先載入，否則閒置回收會停止 Hangfire Server。

## 內建作業

| 作業 ID | 說明 | 預設 Cron | 呼叫端點 | Scope |
|---|---|---|---|---|
| `database-backup-full` | 資料庫完整備份 + RESTORE VERIFYONLY | `0 2 * * *` | `POST api/v1/database-maintenance/backups` | `database.backup` |
| `database-backup-differential` | 差異備份（預設停用） | `0 13 * * *` | 同上 (`differential: true`) | `database.backup` |
| `access-review-campaign` | 建立本期權限覆核活動（同期間冪等） | `0 8 1 1,4,7,10 *` | `POST api/v1/access-reviews/campaigns/scheduled` | `accessreview.campaign.schedule` |
| `log-cleanup` | 依 `Scheduler:LogCleanup` 保留天數分批清除過期紀錄 | `30 3 * * *` | `POST api/v1/database-maintenance/log-cleanup` | `logs.cleanup` |

週期優先順序：排程管理頁自訂（`core.ScheduledJobSettings`）→ `Scheduler:Jobs:{Id}:Cron` → 程式預設。自訂週期最短間隔 5 分鐘。

日誌清除範圍：Outbox（已處理 / 死信）、排程執行紀錄、管理異動軌跡（預設不清，啟用時至少 365 天）。
`SystemLogs`、`SecurityLog`、`AuditLogs` 為 SQL Server 僅附加 Ledger 資料表，資料庫禁止 DELETE，不在清除範圍（結果中列出筆數）。

## 啟用步驟

1. DBA 執行 `docs/sql/20261028_grant_scheduler_hangfire_backup.sql`（hangfire Schema DML、備份專用帳號）。
2. 「系統帳號管理」建立帳號（例如 `scheduler`），授予上表 Scope（新增作業時記得補授 Scope），記下 ClientId / ClientSecret。
3. 設定（金鑰以環境變數或 `claudesettings.json` 注入）：
   ```
   Scheduler__Enabled=true
   Scheduler__ApiBaseAddress=https://physlims-api/
   Scheduler__SystemAccount__ClientId=...
   Scheduler__SystemAccount__ClientSecret=...
   PersistentSettings__ConnectionStrings__BackupConnection=...
   ```
4. 以 `GET api/v1/scheduler/jobs` 檢視狀態，`POST api/v1/scheduler/jobs/{id}/trigger` 立即執行。

## 自訂作業

```csharp
public sealed class InventorySyncJob(SchedulerApiClient api) : IRecurringJob
{
    [JobDisplayName("庫存同步")]
    [AutomaticRetry(Attempts = 3)]
    public async Task<string?> ExecuteAsync(CancellationToken ct)
    {
        var result = await api.PostAsync<SyncResult>("api/v1/inventory/sync", null, TimeSpan.FromMinutes(5), ct);
        return $"同步 {result.Count} 筆";   // 執行摘要，顯示於執行紀錄
    }
}

builder.Services.AddRecurringJob<InventorySyncJob>("inventory-sync", "庫存同步", "*/30 * * * *");
```

目標端點須標註 `[RequiresSystemScope("inventory.sync")]`，並將該 Scope 授予排程系統帳號。
