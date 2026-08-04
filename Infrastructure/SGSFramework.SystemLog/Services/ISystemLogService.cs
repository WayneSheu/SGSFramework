namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 數據入口點」的角色。它不僅僅是把資料存進資料庫，更是觸發後續即時推播與自動告警的火車頭
    /// 持久化 (Persistence)：將日誌寫入 SQL Server 供日後審計。
    /// 分發(Broadcasting)：透過 SignalR 將日誌推送到管理員的 Dashboard。
    /// 過濾與告警(Filtering & Alerting)：識別 LogLevel == Error 的紀錄並觸發外部通知（LINE/Telegram）。
    /// </summary>
    public interface ISystemLogService
    {
        /// <summary>
        /// 寫入日誌
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        Task WriteLogAsync(Core.Abstractions.Logings.SystemLog log);
    }
}