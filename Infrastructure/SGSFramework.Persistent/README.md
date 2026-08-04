# SGSFramework.Persistent

[![NuGet](https://img.shields.io/badge/nuget-v1.0.3-blue.svg)](http://localhost:5000)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com)

`SGSFramework.Persistent` 是架構的**資料持久化與基礎建設核心組件**。本組件基於 Entity Framework Core 10 與 Microsoft SQL Server 2025 進行建構，內建支援原生向量搜尋 (Vector Search)、資料庫總帳 (Ledger) 功能、自動化稽核軌跡 (Audit Log) 攔截，以及全域虛擬刪除 (Soft Delete) 篩選器。

---

## 🛠️ 技術堆疊

* **開發框架**：.NET 10.0 (C# 14 strict null checks)
* **ORM 框架**：Entity Framework Core 10
* **目標資料庫**：Microsoft SQL Server 2025 (含 Vector & Ledger 特性)

---

## 🚀 快速開始

### 1. 安裝套件
確保您的環境已正確設定私有套件來源 `MyPrivateBaGet`，接著在專案目錄下執行以下指令：

###bash
dotnet add package SGSFramework.Persistent --source MyPrivateBaGet

2. 組態配置 (appsettings.json)
請在裝載應用程式（如 Web API 或 Worker Service）的 appsettings.json 中配置連線字串與持久化層相關參數：
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=D3MTG\\SQL2025;Database=SES_DB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "PersistenceOptions": {
    "EnableSensitiveDataLogging": false,
    "EnableDetailedErrors": true
  }
}
3. 相依性注入 (DI) 註冊
在應用程式的進入點（如 Program.cs）中，引入擴充方法以優化初始化流程：
using SGSFramework.Persistent.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 核心功能：注入資料庫上下文、微服務 Unit of Work 與所有內建 Repository
builder.Services.AddPersistentInfrastructure(builder.Configuration);

var app = builder.Build();
📦 核心架構與設計模式
本組件依循 Domain-Driven Design (DDD) 的 Clean Architecture 規範設計，並整合以下核心機制：

1. 稽核軌跡自動化 (Audit Logging)
組件內部註冊了自訂的 AuditLogSaveChangesInterceptor。任何繼承自 IAuditableEntity 的領域實體，在執行 SaveChanges 或 SaveChangesAsync 時，系統會自動擷取以下行為並寫入稽核總帳表：

CreatedBy / CreatedAt (新增)

LastModifiedBy / LastModifiedAt (修改)

2. 虛擬刪除全域篩選器 (Soft Delete)
實作 ISoftDelete 介面的實體，當呼叫 DbContext.Remove() 時，攔截器會自動將其轉換為邏輯刪除 (IsDeleted = true)。全域查詢篩選器 (Global Query Filter) 預設會過濾掉已被刪除的資料。

若需讀取包含已刪除的資料：請在 LINQ 查詢中使用 .IgnoreQueryFilters()。

3. 多權限點延伸支援 (Bitmask Permission)
針對系統權限點預期超過 64 個之架構需求，本持久化層針對權限欄位提供特殊對應支援，確保高擴充性權限矩陣能正確進行欄位持久化。

⚠️ 錯誤處理與防禦性程式碼
本組件嚴格遵循 C# 10+ 的強烈 Null 安全性檢查 (Nullable Reference Types)。

當執行資料庫存取發生異常時，組件會將底層 SqlException 封裝為具備領域語意的 PersistenceException。

呼叫資料庫讀取方法（如 GetByIdAsync）時，若查無資料，傳回值將明確定義為 Nullable<T>，請調用方務必進行 Null 安全性檢查。
// 呼叫範例
var result = await _repository.GetByIdAsync(id);
if (result is null)
{
    // 依業務邏輯處理 NotFound 狀況
}

👥 團隊維護與支援
主要維護者：Wayne

聯絡管道：專案團隊內部通訊 / Git 議題追蹤系統

💡 提醒：若在開發期間遇到 Named Pipes Provider (error: 40) 等連線問題，請優先確認本機 SQL Server 設定管理員中的 TCP/IP 通訊協定是否已啟用。
