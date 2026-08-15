#nullable enable
using System;
using SGSFramework.Core.Helpers;

namespace SGSFramework.Core.Abstractions.Outbox;

/// <summary>
/// Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// 訊息唯一識別碼
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 關聯 ID：代表整個業務流程的追蹤編號 (由前端或 API 傳入)
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 誘發 ID：代表觸發此事件的具體 Command 名稱或 ID
    /// </summary>
    public string? CausationId { get; set; }

    /// <summary>
    /// 事件的完整型別名稱 (包含 Namespace)，用於還原物件
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 事件序列化後的 JSON 內容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 事件發生的原始時間 (UTC)
    /// </summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>
    /// 訊息被成功處理的時間。
    /// 若為 NULL 且 IsDead 為 false，表示待處理；
    /// 若為 1900-01-01，表示正被某個 Worker 鎖定處理中。
    /// </summary>
    public DateTime? ProcessedOnUtc { get; set; }

    /// <summary>
    /// 預計下次重試的時間 (UTC)。
    /// 用於實作「指數退避」，避免失敗後立即重試造成系統壓力。
    /// </summary>
    public DateTime? ScheduledAtUtc { get; set; }

    /// <summary>
    /// 目前已重試的次數
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// 是否已成為「死信」(Dead Letter)。
    /// 當 RetryCount 超過閾值後標記為 true，背景服務將不再主動抓取。
    /// </summary>
    public bool IsDead { get; set; } = false;

    /// <summary>
    /// 最後一次處理失敗的錯誤訊息內容，便於維運排錯
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// 建立新的 Outbox 訊息 (建構子)
    /// </summary>
    public OutboxMessage()
    {
        Id = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// 標記訊息處理失敗，並套用指數退避與死信邏輯
    /// </summary>
    /// <param name="errorMessage">發生的錯誤訊息</param>
    /// <param name="maxRetries">專案配置的最大重試次數</param>
    public void MarkAsFailed(string errorMessage, int maxRetries)
    {
        LastError = errorMessage;
        RetryCount++;

        // 釋放鎖定，讓 ProcessedOnUtc 變回 null，否則 SQL 的 WHERE 子句抓不到
        ProcessedOnUtc = null;

        if (RetryCount >= maxRetries)
        {
            IsDead = true;
            ScheduledAtUtc = null; // 變成死信後不再需要排程
        }
        else
        {
            // 呼叫 BackoffHelper 計算下次重試時間
            ScheduledAtUtc = BackoffHelper.CalculateNextRetryTime(RetryCount);
        }
    }

    /// <summary>
    /// 標記訊息處理成功
    /// </summary>
    public void MarkAsProcessed()
    {
        ProcessedOnUtc = DateTime.UtcNow;
        LastError = null;
    }
}