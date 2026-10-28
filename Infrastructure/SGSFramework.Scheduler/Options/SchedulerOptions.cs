using System.ComponentModel.DataAnnotations;
using Enterprise.HttpSdk.Options;

namespace SGSFramework.Scheduler.Options;

/// <summary>
/// 排程框架組態 (區段 Scheduler)。
/// 排程作業一律以系統帳號 (Client Credentials) 換發的權杖呼叫內部 API，ClientSecret 嚴禁寫入 appsettings，
/// 請以環境變數 Scheduler__SystemAccount__ClientSecret 或 claudesettings.json 注入。
/// </summary>
public sealed class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    /// <summary>是否啟用 Hangfire 排程（關閉時仍保留內部端點，可改由 K8s CronJob 等外部排程呼叫）</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 是否於本節點啟動 Hangfire Server（執行作業）。多節點 / K8s 可僅於專用 Worker 開啟；
    /// 關閉時本節點只負責註冊週期作業與提供管理 API。
    /// </summary>
    public bool ServerEnabled { get; set; } = true;

    /// <summary>週期作業的預設時區 (IANA 或 Windows ID)；找不到時使用主機時區</summary>
    public string TimeZone { get; set; } = "Asia/Taipei";

    /// <summary>內部 API 位址（排程作業呼叫的目標），例如 https://localhost:7253/ 或 K8s 內部網域</summary>
    public Uri? ApiBaseAddress { get; set; }

    /// <summary>排程服務使用的系統帳號；TokenEndpoint 未設定時預設為 {ApiBaseAddress}api/v1/system-accounts/token</summary>
    public SystemAccountClientOptions SystemAccount { get; set; } = new();

    public SchedulerStorageOptions Storage { get; set; } = new();

    public SchedulerServerOptions Server { get; set; } = new();

    public SchedulerDashboardOptions Dashboard { get; set; } = new();

    /// <summary>
    /// 各週期作業的覆寫設定 (Key = 作業 ID，例如 database-backup-full)；未設定者使用程式註冊時的預設值
    /// </summary>
    public Dictionary<string, RecurringJobSettings> Jobs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DatabaseBackupOptions DatabaseBackup { get; set; } = new();

    public LogCleanupOptions LogCleanup { get; set; } = new();

    public AccessReviewScheduleOptions AccessReview { get; set; } = new();
}

public sealed class SchedulerStorageOptions
{
    /// <summary>Hangfire 資料表所在 Schema</summary>
    [Required(AllowEmptyStrings = false)]
    public string SchemaName { get; set; } = "hangfire";

    /// <summary>
    /// 啟動時是否建立 / 升級 Hangfire 資料表。結構異動使用 MigrationConnection (部署帳號)，
    /// 執行期一律使用 DefaultConnection (最小權限帳號，需授予 hangfire Schema 的 DML 權限)。
    /// </summary>
    public bool PrepareSchemaIfNecessary { get; set; } = true;
}

public sealed class SchedulerServerOptions
{
    /// <summary>同時執行的作業數；0 表示使用 Hangfire 預設值 (Min(處理器數 × 5, 20))</summary>
    [Range(0, 100)]
    public int WorkerCount { get; set; } = 5;

    /// <summary>監聽的佇列；空白時為 default。預設值必須為空陣列：組態繫結會「附加」到既有陣列，預設含 default 會變成重複監聽</summary>
    public string[] Queues { get; set; } = [];
}

public sealed class SchedulerDashboardOptions
{
    /// <summary>是否開放 Hangfire Dashboard；正式環境建議關閉，改用排程管理 API</summary>
    public bool Enabled { get; set; }

    public string Path { get; set; } = "/hangfire";

    /// <summary>
    /// 僅允許本機存取。關閉時須為已驗證的系統管理員（IsSuperAdmin），
    /// 由於 API 採 JWT Bearer，瀏覽器直接開啟通常無法帶入權杖，故預設僅本機。
    /// </summary>
    public bool LocalRequestsOnly { get; set; } = true;

    /// <summary>唯讀模式：不可於 Dashboard 觸發 / 刪除作業</summary>
    public bool ReadOnly { get; set; } = true;
}

/// <summary>週期作業覆寫設定</summary>
public sealed class RecurringJobSettings
{
    /// <summary>null 表示沿用程式註冊時的預設值</summary>
    public bool? Enabled { get; set; }

    /// <summary>Cron 運算式（5 碼，Hangfire 格式）</summary>
    public string? Cron { get; set; }

    /// <summary>覆寫時區</summary>
    public string? TimeZone { get; set; }
}

/// <summary>資料庫備份設定（區段 Scheduler:DatabaseBackup）</summary>
public sealed class DatabaseBackupOptions
{
    /// <summary>
    /// 備份目錄（SQL Server 主機上的路徑或 UNC）；空白時使用執行個體預設備份目錄 (InstanceDefaultBackupPath)。
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>COPY_ONLY：不影響既有的備份鏈（已有 DBA 備份策略時建議開啟）</summary>
    public bool CopyOnly { get; set; }

    /// <summary>備份壓縮（SQL Server Express 不支援）</summary>
    public bool Compression { get; set; }

    /// <summary>WITH CHECKSUM 檢查頁面完整性</summary>
    public bool Checksum { get; set; } = true;

    /// <summary>備份後執行 RESTORE VERIFYONLY 驗證備份檔可還原</summary>
    public bool Verify { get; set; } = true;

    /// <summary>
    /// 保留天數：大於 0 時刪除本系統產生且超過天數的備份檔（僅限應用程式主機可存取該目錄時，如 UNC 共用）；0 表示不清理。
    /// </summary>
    [Range(0, 3650)]
    public int RetentionDays { get; set; }

    /// <summary>BACKUP / VERIFY 指令逾時秒數</summary>
    [Range(60, 86400)]
    public int CommandTimeoutSeconds { get; set; } = 3600;
}

/// <summary>
/// 日誌清除設定（區段 Scheduler:LogCleanup）。各項 0 表示不清除。
/// 系統日誌、資安日誌、稽核日誌 (AuditLogs) 為 SQL Server 僅附加 Ledger 資料表，資料庫禁止刪除，不在清除範圍。
/// </summary>
public sealed class LogCleanupOptions
{
    /// <summary>已處理的 Outbox 訊息 (core / org) 保留天數</summary>
    [Range(0, 3650)]
    public int OutboxProcessedRetentionDays { get; set; } = 30;

    /// <summary>無法投遞的 Outbox 死信保留天數（保留較久供追查）</summary>
    [Range(0, 3650)]
    public int OutboxDeadLetterRetentionDays { get; set; } = 90;

    /// <summary>排程執行紀錄 (core.ScheduledJobRuns) 保留天數</summary>
    [Range(0, 3650)]
    public int ScheduledJobRunRetentionDays { get; set; } = 180;

    /// <summary>
    /// 管理異動軌跡（角色、系統帳號、系統配置、功能狀態異動紀錄）保留天數；屬稽核證據，預設不清除，啟用時至少 365 天
    /// </summary>
    [Range(0, 3650)]
    public int AuditTrailRetentionDays { get; set; }

    /// <summary>每批刪除筆數（分批避免長時間鎖表與交易記錄暴增）</summary>
    [Range(100, 50000)]
    public int BatchSize { get; set; } = 5000;
}

/// <summary>定期權限覆核設定（區段 Scheduler:AccessReview）</summary>
public sealed class AccessReviewScheduleOptions
{
    /// <summary>活動名稱前綴；完整名稱 = 前綴 + 期間（例如「定期權限覆核 2026-Q4」），同期間重複執行不會重複建立</summary>
    [Required(AllowEmptyStrings = false)]
    public string NamePrefix { get; set; } = "定期權限覆核";

    /// <summary>期間命名粒度，應與 Cron 頻率一致</summary>
    public AccessReviewPeriod Period { get; set; } = AccessReviewPeriod.Quarter;

    public string? Description { get; set; } = "由排程服務自動建立的定期權限覆核活動。";

    /// <summary>覆核期限（天）</summary>
    [Range(1, 365)]
    public int DueInDays { get; set; } = 14;

    /// <summary>覆核範圍角色 ID；空白表示全部角色</summary>
    public List<Guid> RoleIds { get; set; } = [];

    /// <summary>建立新活動前，先結束已逾期的覆核活動（未覆核項目保留於報表供追蹤）</summary>
    public bool CloseOverdueCampaigns { get; set; }
}

public enum AccessReviewPeriod
{
    Month = 0,
    Quarter = 1,
    HalfYear = 2,
    Year = 3
}
