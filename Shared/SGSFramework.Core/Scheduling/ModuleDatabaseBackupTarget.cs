using System.Text.RegularExpressions;

namespace SGSFramework.Core.Scheduling;

/// <summary>
/// 外掛模組自有資料庫的備份宣告（主資料庫 PhysLIMSDB 內的模組 schema 已涵蓋於主庫備份，不需宣告）。
/// <para>
/// 模組於 <c>RegisterDependencies</c> 以 <c>services.AddSingleton(new ModuleDatabaseBackupTarget(...))</c> 註冊；
/// 排程模組 (SGS.Modules.SCH) 為每個目標產生獨立的完整 / 差異備份週期作業（作業 ID：database-backup-full-{Key}、
/// database-backup-differential-{Key}），可於排程管理頁個別設定週期、暫停與檢視紀錄。
/// </para>
/// <para>
/// 備份一律使用 PersistentSettings:ConnectionStrings:BackupConnection 的登入帳號，僅將資料庫切換為本目標的資料庫；
/// 該登入須為目標資料庫的 db_backupoperator，且目標資料庫須與主庫位於同一 SQL Server 執行個體。
/// </para>
/// 型別位於 Core（Default ALC 共享），各外掛註冊的實例可被排程外掛直接解析。
/// </summary>
/// <param name="Key">識別碼（小寫英數與連字號，例如 log），用於作業 ID 與備份 API 的 target 參數</param>
/// <param name="DisplayName">顯示名稱（例如「日誌資料庫」）</param>
/// <param name="ConnectionStringKey">取得資料庫名稱的組態鍵（例如 PersistentSettings:ConnectionStrings:LogConnection）</param>
/// <param name="DefaultFullCron">完整備份預設週期（可由排程管理頁或 Scheduler:Jobs 覆寫）</param>
/// <param name="DefaultDifferentialCron">差異備份預設週期（預設停用，與主庫相同）</param>
public sealed partial record ModuleDatabaseBackupTarget(
    string Key,
    string DisplayName,
    string ConnectionStringKey,
    string DefaultFullCron = "30 2 * * *",
    string DefaultDifferentialCron = "30 13 * * *")
{
    /// <summary>Key 格式：小寫英數，以單一連字號分隔（不可含冒號，否則無法作為 Scheduler:Jobs 組態鍵）</summary>
    public static bool IsValidKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
