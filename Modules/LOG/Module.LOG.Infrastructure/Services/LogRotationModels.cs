namespace SGS.Modules.LOG.Infrastructure.Services;

/// <summary>年度換庫的設定鍵（連線字串以環境變數注入，不得寫入版控）</summary>
public static class LogRotationOptions
{
    /// <summary>換庫專用帳號 rotate_sgs_user（docs/sql/20261110_grant_log_rotation_account.sql）；須與其他連線使用不同 SQL 帳號</summary>
    public const string ConnectionKey = "PersistentSettings:ConnectionStrings:RotationConnection";

    /// <summary>備份檔目錄（SQL Server 主機上的目錄，SQL Server 服務帳號需可寫入）；未設定時使用執行個體預設備份目錄</summary>
    public const string BackupDirectoryKey = "LogRotation:BackupDirectory";

    /// <summary>單一 SQL 指令逾時秒數（備份 / 還原 / 驗證），預設 3600，限 60 至 14400</summary>
    public const string CommandTimeoutKey = "LogRotation:CommandTimeoutSeconds";

    /// <summary>預設換庫粒度：Year（整年度，預設）或 HalfYear（半年度）。影響建議的封存期間與排程準備的期間；手動準備可任意指定年度 / 上半年 / 下半年</summary>
    public const string GranularityKey = "LogRotation:Granularity";
}

/// <summary>換庫作業失敗（預期內的錯誤：未設定、權限不足、前置條件不符、核對不一致）；訊息為繁體中文，可直接顯示給管理者</summary>
public sealed class LogRotationException : Exception
{
    public LogRotationException(string message) : base(message) { }

    public LogRotationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>換庫階段：Idle 無進行中；Staged 冷庫已備妥待核准；HotMissing 熱庫不存在（上次換庫在刪除後中斷，需核准以重建熱庫）</summary>
public static class LogRotationPhase
{
    public const string Idle = "Idle";
    public const string Staged = "Staged";
    public const string HotMissing = "HotMissing";
}

/// <param name="Period">期間後綴：2026、2026H1、2026H2</param>
public sealed record LogRotationStagedInfo(string Database, string Period, string PeriodDisplay);

public sealed record LogRotationColdInfo(string Key, string Database, string DisplayName);

/// <param name="Configured">是否已設定換庫帳號連線且帳號分離</param>
/// <param name="Problem">設定或連線問題說明（無問題為 null）</param>
/// <param name="Granularity">預設粒度：Year / HalfYear</param>
/// <param name="SuggestedPeriod">建議的封存期間後綴（已跳過重疊的既有冷庫）</param>
/// <param name="SuggestedNote">建議期間因既有冷庫而往後推時的說明（無則為 null）</param>
/// <param name="Phase"><see cref="LogRotationPhase"/></param>
/// <param name="Busy">是否有換庫作業正在執行（任一節點）</param>
public sealed record LogRotationStatus(
    bool Configured,
    string? Problem,
    string HotDatabase,
    bool HotExists,
    bool HotOnline,
    string Granularity,
    string SuggestedPeriod,
    string SuggestedPeriodDisplay,
    string SuggestedColdDatabase,
    string? SuggestedNote,
    string Phase,
    bool Busy,
    LogRotationStagedInfo? Staged,
    IReadOnlyList<LogRotationColdInfo> Colds);

/// <summary>準備階段結果：冷庫已以完整備份還原並停在「還原中」，等待管理者核准</summary>
public sealed record LogRotationPrepareResult(
    string StagedDatabase,
    string Period,
    string PeriodDisplay,
    string BackupFile,
    TimeSpan Duration,
    IReadOnlyList<string> Steps);

/// <param name="UsedFallbackFullBackup">差異備份無法銜接時，改以凍結期間重新完整備份（較慢）</param>
/// <param name="RowCounts">資料表名稱 → 冷庫筆數（已與熱庫核對一致）</param>
public sealed record LogRotationFinalizeResult(
    string ColdDatabase,
    string Period,
    string PeriodDisplay,
    bool UsedFallbackFullBackup,
    bool HotRecreated,
    IReadOnlyDictionary<string, long> RowCounts,
    TimeSpan Duration,
    IReadOnlyList<string> Steps);
