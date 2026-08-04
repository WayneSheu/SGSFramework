SGSFramework.SystemLog
SGSFramework.SystemLog 是 SGSFramework 系統的全域系統診斷與事件日誌組件。與 SGSFramework.AuditLog（關注業務資料變更）不同，本組件主要負責系統運行時的診斷訊息、效能監控日誌與運作錯誤追蹤。

🛠️ 技術堆疊
開發框架：.NET 10.0 (C# 14 strict null checks)

日誌標準：Microsoft.Extensions.Logging (Abstractions)

儲存機制：支援多目標輸出 (Structured Logging)

🚀 組件功能特色
1. 結構化日誌 (Structured Logging)
所有日誌皆以結構化格式 (JSON) 儲存，方便日後匯出至 Logstash、ELK Stack 或 Azure Monitor 進行大數據分析。

2. 多層級診斷分類
本組件定義了嚴格的日誌等級分類：

Critical：系統崩潰或關鍵服務中斷。

Error：業務邏輯執行失敗或未預期的例外。

Warning：潛在的效能瓶頸或已恢復的非預期狀態。

Information：關鍵生命週期事件（如：模組啟動、作業排程完成）。

Trace/Debug：開發與調試階段的詳細堆疊追蹤。

3. 情境上下文 (Log Context)
自動注入執行緒 ID、Correlation ID (請求追蹤 ID)、使用者 ID 與目前執行之模組名稱，確保在分散式架構下能輕易串聯完整請求流程。

🏗️ 實作指引
1. 注入使用
透過標準 DI 容器注入 ISystemLogger：

C#
public class OrderService(ISystemLogger logger)
{
    public void ProcessOrder(Guid orderId)
    {
        logger.LogInfo("開始處理訂單", new { OrderId = orderId });
        try { /* ... */ }
        catch (Exception ex) { logger.LogError("訂單處理失敗", ex); }
    }
}
2. 擴充設定
在 appsettings.json 中配置過濾規則：

JSON
{
  "SystemLogOptions": {
    "MinimumLevel": "Information",
    "EnablePerformanceMetrics": true, // 是否自動記錄 API 回應時間
    "LogSink": "SqlServer" // 支援 SqlServer, Console, File
  }
}
⚠️ 架構規範
效能優化：本組件採用非同步寫入機制 (Async Sink)，確保日誌記錄動作不會阻塞核心業務執行緒。

敏感資訊脫敏 (Masking)：組件內建自動遮罩機制，針對 Password, CreditCard, Token 等關鍵字進行即時處理，避免洩漏機敏資訊。

儲存策略：鑑於系統運作日誌數量龐大，建議設定 SQL Server 的自動清除排程（如保留 30 天以內的除錯日誌），以避免資料庫膨脹。

👥 團隊維護與支援
主要維護者：Wayne

技術協作：請與 SGSFramework.Persistent 的錯誤處理攔截機制配合使用。

💡 提醒：若系統出現大規模異常，請優先查看 Correlation ID 以串連不同模組間的日誌片段。