using System.ComponentModel;

namespace SGSFramework.Core.Scheduling;

/// <summary>
/// 排程作業派送契約：Hangfire 週期 / 手動作業一律序列化為此介面的呼叫，執行時由 DI 解析排程外掛（SGS.Modules.SCH）的實作，
/// 再依作業 ID 找出實際的作業類別執行。
/// <para>
/// 介面置於 Core（Default ALC 共享），Hangfire 以型別名稱反序列化作業時一定解析得到；
/// 外掛模組自身的型別位於獨立 AssemblyLoadContext，不可直接交由 Hangfire 以名稱解析。
/// </para>
/// </summary>
public interface IScheduledJobDispatcher
{
    /// <param name="jobId">週期作業 ID（例如 database-backup-full）</param>
    /// <param name="cancellationToken">註冊時傳入 CancellationToken.None，執行期由 Hangfire 替換為作業取消權杖</param>
    /// <returns>執行摘要，記錄於執行紀錄</returns>
    [DisplayName("排程作業：{0}")]
    Task<string?> ExecuteAsync(string jobId, CancellationToken cancellationToken);
}
