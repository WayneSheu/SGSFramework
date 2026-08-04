SGSFramework.AuditLog
SGSFramework.AuditLog 是 架構的自動化稽核軌跡追蹤組件。本組件設計為無侵入式 (Non-intrusive) 的攔截器，專門負責擷取、序列化並持久化系統內的領域變更事件，滿足企業合規性與資料變更追溯需求。

🛠️ 技術堆疊
開發框架：.NET 10.0 (C# 14 strict null checks)

核心依賴：SSGSFramework.Core, SGSFramework.Persistent (透過 Interceptor 機制)

儲存目標：自動對應至底層 Audit 總帳資料表

🚀 組件功能特色
1. 攔截器模式 (Interceptor Pattern)
透過整合 Microsoft.EntityFrameworkCore 的 SaveChangesInterceptor，本組件在資料庫交易提交前，自動偵測已變更的實體。

2. 變更差異比對 (Delta Tracking)
支援自動紀錄：

舊值 (Original Value) 與 新值 (Current Value) 的差異對比。

變更類型：Added, Modified, Deleted。

執行者與時間戳記：自動關聯 IAuditableEntity 介面。

3. 自動化序列化
將實體的變更細節以 Json 格式進行序列化儲存，確保稽核紀錄在結構變更時仍具備高彈性。

🏗️ 實作指引
1. 啟用稽核追蹤
確保您的領域實體繼承自 IAuditableEntity 或 BaseEntity，並啟用稽核攔截器：

C#
// 在 SGSFramework.Persistent 的 DbContext 配置中註冊攔截器
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.AddInterceptors(new AuditLogSaveChangesInterceptor(_auditService));
}
2. 實體擴充
只需實作介面，無需手動撰寫紀錄程式碼：

C#
public class Order : BaseEntity, IAuditableEntity
{
    public string Status { get; set; }
    // 其他業務屬性...
}
⚠️ 架構規範與安全性
效能考量：稽核紀錄預設在同一交易 (Transaction) 內執行。若系統寫入頻率極高，建議調整為非同步隊列 (Background Queue) 處理。

機敏資料過濾：組件內建 [AuditIgnore] 屬性，標註此屬性的欄位將不會被序列化至稽核總帳中，以保護個資（如密碼、信用卡號）。

資料一致性：稽核紀錄與原始資料的寫入位於同一資料庫交易，確保「資料變更」與「稽核紀錄」不可分割。

👥 團隊維護與支援
主要維護者：Wayne

適用環境：需配合 SGSFramework.Persistent 共同使用。

💡 提醒：稽核資料量會隨著系統運作呈線性增長。建議針對 Audit 資料表設定 SQL Server 的資料分割 (Partitioning) 或定期執行封存 (Archiving) 策略，以維持查詢效能。