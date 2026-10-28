namespace SGSFramework.Scheduler.Abstractions;

/// <summary>
/// 週期排程作業。實作類別以 <c>AddRecurringJob&lt;TJob&gt;</c> 註冊，由 Hangfire 以 DI Scope 建立並執行。
/// <para>
/// 作業應保持「薄」：僅負責以系統帳號呼叫內部 API（標註 RequiresSystemScope 的端點），
/// 業務邏輯、授權、稽核留在 API 端，使排程可在任一節點或外部排程器執行。
/// </para>
/// </summary>
public interface IRecurringJob
{
    /// <param name="cancellationToken">Hangfire 於伺服器關閉或作業刪除時取消（註冊時傳入 CancellationToken.None，執行期由 Hangfire 替換）</param>
    /// <returns>執行摘要（例如「清除 120 筆」），記錄於執行紀錄；失敗請拋出例外</returns>
    Task<string?> ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>週期作業的註冊資訊（程式預設值，可由 Scheduler:Jobs:{Id} 覆寫）</summary>
/// <param name="Id">作業 ID（Hangfire Recurring Job ID）</param>
/// <param name="JobType">實作 <see cref="IRecurringJob"/> 的型別</param>
/// <param name="DisplayName">顯示名稱</param>
/// <param name="DefaultCron">預設 Cron</param>
/// <param name="EnabledByDefault">未設定覆寫時是否啟用</param>
public sealed record RecurringJobDescriptor(
    string Id,
    Type JobType,
    string DisplayName,
    string DefaultCron,
    bool EnabledByDefault);
