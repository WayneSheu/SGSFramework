# SGS.Modules.SCH（排程管理外掛模組）

以 Hangfire (SQL Server Storage) 實作的排程管理，整合系統帳號 (M2M) 執行週期作業，並提供資料庫維護（備份、日誌清除）API。
2026-11-04 由 Host 端的 `SGSFramework.Scheduler` 改為外掛模組。

## 外掛模組結構

| 專案 | 內容 |
| --- | --- |
| `Module.SCH`（SGS.Modules.SCH） | `SchedulerController`（`api/v1/scheduler`）、`DatabaseMaintenanceController`（`api/v1/database-maintenance`）、`ModuleSchInitializer` |
| `Module.SCH.Application` | Hangfire 註冊、內建作業、`ScheduledJobManager`、`ScheduledJobDispatcher`、執行紀錄篩選器、`SchedulerApiClient`、備份 / 日誌清除服務、組態 |
| `Module.SCH.Infrastructure` | `SCHDbContext`（schema `sch`：`ScheduledJobSettings`、`ScheduledJobRuns`、`AuditLogs`、`OutboxMessages`）、`SchMigrationService`、Migrations |

- plugins 目錄只部署 `SGS.Modules.SCH*.dll`。Hangfire、Cronos、Enterprise.HttpSdk 由 Host 主程式目錄提供（PhysLIMS.API 參考本模組時自動輸出），放進 plugins 會因簽章驗證被隔離。
- 組態區段維持 `Scheduler`（Host appsettings / 環境變數 `Scheduler__*`），部署腳本不需調整。
- 啟動時由 `ModuleSchInitializer` 以 MigrationConnection 套用 `sch` 遷移、授予執行期帳號 `sch` DML，並對應 Hangfire Dashboard。
- 模組含 Hangfire Server（Hosted Service），**不支援執行中熱抽換**，更新模組須重新啟動 API。

### Hangfire 與外掛 ALC

外掛型別位於獨立 AssemblyLoadContext，Hangfire 以型別名稱反序列化作業時無法可靠解析。因此：

- 所有週期 / 手動作業一律序列化為 Core 契約 `SGSFramework.Core.Scheduling.IScheduledJobDispatcher.ExecuteAsync(jobId, ct)`（Default ALC 共享）。
- 執行時 DI 解析本模組的 `ScheduledJobDispatcher`，依作業 ID 建立實際的 `IRecurringJob` 類別。
- 作業類別上的 `[AutomaticRetry]` 由 `ScheduledJobFilterProvider` 依作業 ID 套用；`[DisableConcurrentExecution]` 改以作業 ID 為鎖定資源（原 Attribute 以方法名稱為資源，所有作業會互相排斥）。
- 舊版（`SGSFramework.Scheduler.Jobs.*` 型別）的週期作業於啟動時以同 ID 覆寫；若 Hangfire 仍有舊型別的排隊 / 重試作業，會因型別不存在而失敗，可於 Dashboard 刪除。

## 架構

```
Hangfire 週期作業 → IScheduledJobDispatcher (Core) → IRecurringJob (本模組)
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
| `file-purge` | 已刪除檔案清除（`FileManagement:Enabled` 時註冊） | `0 4 * * *` | `POST api/v1/files/purge`（SGS.Modules.FMS） | `files.purge` |

| `database-backup-full-{模組}` / `database-backup-differential-{模組}` | 外掛模組自有資料庫的完整 / 差異備份（見下節） | `30 2 * * *` / `30 13 * * *`（差異預設停用） | `POST api/v1/database-maintenance/backups`（`target: "{模組}"`） | `database.backup` |

### 外掛模組自有資料庫備份

模組 schema 若位於主資料庫（ORG、FMS、RPT、SCH），已由主庫備份涵蓋。擁有**獨立資料庫**的模組（目前為 LOG 的 `PhysLIMSLogDB`）於 `RegisterDependencies` 宣告 Core 契約：

```csharp
services.AddSingleton(new ModuleDatabaseBackupTarget("log", "日誌資料庫", "PersistentSettings:ConnectionStrings:LogConnection"));
```

- 排程模組為每個目標產生獨立的完整 / 差異備份作業，於排程管理頁個別設定週期、暫停與檢視紀錄；模組停用（未註冊目標）時作業不出現。
- 資料庫名稱取自目標的連線字串；備份沿用 `BackupConnection` 的登入帳號，僅切換資料庫。DBA 須執行 `docs/sql/20261105_grant_module_database_backup.sql`，讓 `backup_sgs_user` 成為該資料庫的 `db_backupoperator`；模組資料庫須與主庫位於同一 SQL Server 執行個體。
- 同一資料庫同時只允許一個備份；不同資料庫的備份可並行。目標與主資料庫相同時拒絕（已由主庫備份涵蓋）。
- Key 限小寫英數與連字號（作業 ID 也是 `Scheduler:Jobs` 的組態鍵，不可含冒號）。

週期優先順序：排程管理頁自訂（`sch.ScheduledJobSettings`）→ `Scheduler:Jobs:{Id}:Cron` → 程式預設。自訂週期最短間隔 5 分鐘。

日誌清除範圍：Outbox（已處理 / 死信）、排程執行紀錄（`sch.ScheduledJobRuns`）、管理異動軌跡（預設不清，啟用時至少 365 天）。
`SystemLogs`、`SecurityLog`、`AuditLogs` 為 SQL Server 僅附加 Ledger 資料表，資料庫禁止 DELETE，不在清除範圍（結果中列出筆數）。

## 權限

| 權限碼 | 用途 |
| --- | --- |
| `SCH.SCHEDULER.READ` / `EXECUTE` / `MANAGE` | 檢視、立即執行、暫停 / 恢復 / 設定週期 |
| `SCH.DATABASEMAINTENANCE.BACKUP` / `LOGCLEANUP` | 手動執行資料庫備份、日誌清除 |

選單路徑 `/sgs.modules.sch/scheduler`。舊權限碼 `SYSTEM.SCHEDULER.*`、`SYSTEM.DATABASEMAINTENANCE.*` 的角色授權由 Host migration `MoveScheduledJobTablesToSch` 轉換。

## 資料表移轉

`ScheduledJobRuns` / `ScheduledJobSettings` 原屬 Host 的 `core` schema。Host migration `MoveScheduledJobTablesToSch` 與本模組 `SCHDB_Initial` 均以「存在即 `ALTER SCHEMA TRANSFER`、不存在才建立」處理，執行順序不影響結果、資料完整保留。
**重新產生 `SCHDB_Initial` 時須保留守衛式 SQL**，不可改回 `CreateTable`。

## 啟用步驟

1. DBA 執行 `docs/sql/20261028_grant_scheduler_hangfire_backup.sql`（hangfire Schema DML、備份專用帳號）與 `docs/sql/20261104_grant_sch_schema.sql`（sch Schema）。
2. 「系統帳號管理」建立帳號（例如 `scheduler`），授予上表 Scope（新增作業時記得補授 Scope），記下 ClientId / ClientSecret。
3. 設定（金鑰以環境變數或 `claudesettings.json` 注入）：
   ```
   Scheduler__Enabled=true
   Scheduler__ApiBaseAddress=https://physlims-api/
   Scheduler__SystemAccount__ClientId=...
   Scheduler__SystemAccount__ClientSecret=...
   PersistentSettings__ConnectionStrings__BackupConnection=...
   ```
4. 部署 `plugins/SGS.Modules.SCH*.dll`，以 `GET api/v1/scheduler/jobs` 檢視狀態，`POST api/v1/scheduler/jobs/{id}/trigger` 立即執行。

## 自訂作業

於 `AddModuleSchApplication` 內註冊（作業類別須在本模組內，跨模組功能一律透過 HTTP 呼叫該模組的系統帳號端點）：

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

services.AddRecurringJob<InventorySyncJob>("inventory-sync", "庫存同步", "*/30 * * * *");
```

目標端點須標註 `[RequiresSystemScope("inventory.sync")]`，並將該 Scope 授予排程系統帳號。
