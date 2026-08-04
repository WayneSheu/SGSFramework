SGSFramework.VerifyLedger
一個基於 .NET 10 與 Clean Architecture 規範設計的企業級動態總帳稽核與驗證套件。本套件專為 SQL Server 2025 Ledger (分散式帳本資料表) 設計，提供端到端的自動化完整性校驗，並整合 QuestPDF 動態編譯技術，一鍵生成具備合規性（Compliance）與可追溯性（Traceability）的 PDF 審計報告。

核心功能
🛡️ 無感帳本校驗 (Automated Ledger Verification)：整合 MSSQL sys.sp_verify_database_ledger 核心儲存程序，提供強固的密碼學雜湊鏈結完整性檢查。

🔄 雙泛型動態注入 (Double-Generic Dynamic DI)：動態解析 TContext (DbContext) 與 TEntity (ILedgerEntity)，免除為新資料表重複撰寫 Controller 或 Service 的冗餘代碼。

📑 自主快照生成 (Self-Hosted Digest Generation)：當前端未帶入外部鏈結公報摘要時，系統自動調用 sys.sp_generate_database_ledger_digest 索取當前最新快照進行自主防護校驗。

📊 企業級 PDF 稽核報告：整合 QuestPDF 動態排版，全域阻斷字符缺陷，自動淬取 SHA-256 數位指紋與區塊資訊，輸出符合 ISO 14064-1 等法規查驗標準的防竄改報告。

系統架構分層
本套件遵循 Clean Architecture 進行模組化切分：

Plaintext
SGSFramework.VerifyLedger/
├── Domain/
│   ├── Interfaces/           # ILedgerEntity 核心抽象介面
│   └── ValueObjects/         # LedgerVerificationResult 驗證結果領域物件
├── Application/
│   └── Interfaces/           # ILedgerVerificationService 雙泛型服務合約
├── Infrastructure/
│   ├── Services/             # MssqlLedgerVerificationService (MSSQL 總帳核心實作)
│   ├── Reporting/            # LedgerPdfReportGenerator (QuestPDF 報表引擎)
│   └── DependencyInjection/  # LedgerServiceCollectionExtensions (DI 註冊擴充)
└── Presentation/
    └── Controllers/          # LedgerVerificationController (動態路由反射控制器)
快速開始
1. 實體宣告
讓需要啟用防竄改總帳驗證的 Domain 實體實作 ILedgerEntity 標記介面：

C#
using Enterprise.LedgerEngine.Domain.Interfaces;

namespace SES.Core.Abstractions.Logings;

/// <summary>
/// 安全稽核日誌總帳實體 (對應 MSSQL Append-Only Ledger Table)
/// </summary>
public class SecurityLog : ILedgerEntity
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
2. 相依性注入註冊
在 Presentation 層的 Program.cs 檔案中，呼叫擴充方法註冊開放式泛型服務：

C#
using SES.SystemLog.VerifyLedger.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 註冊開放式泛型總帳驗證核心元件
builder.Services.AddLedgerVerificationServices();

var app = builder.Build();
3. API 端點使用說明
套件啟用後，將會自動暴露出標準動態路由端點：

A. 執行即時總帳校驗 (JSON 回應)
HTTP Method: POST

URL Route: /api/ledger/{contextName}/verify/{entityName}

Request Body (Optional): 可傳入外部冷儲存之公報摘要 JSON。若為空，系統將自動向 SQL Server 索取最新快照。

B. 下載 PDF 稽核審計報告
HTTP Method: GET

URL Route: /api/ledger/{contextName}/report/{entityName}

Response: application/pdf 檔案串流。

PDF 稽核報告內容規範
生成的 PDF 報告嚴格對應審計法規，主要包含以下三個核心區塊：

驗證狀態摘要 (Verification Status Summary)

明確顯示 isSuccess: true/false 狀態。

提供技術解讀：佐證自區塊創立以來，目標數據未遭受越權或非法外力干預。

驗證詳細資訊 (Audit Metadata)

受稽核資料表 (Target Table)：精確追溯本次校驗的單一實體標的。

驗證回應訊息：明確標記目前已驗證通過的最高區塊序號 (Block ID)。

驗證時間戳記：完整記錄驗證完成的確切 UTC 時間。

摘要內容分析 (Extracted Digest Analysis)

以結構化表格（Table）拆解數位公報細節，包含 database_name、最高 block_id、該區塊的 SHA-256 數位指紋 (Hash)，以及最後交易提交時間（last_transaction_commit_time）。

異常與安全性警報機制
當帳本資料鏈結遭遇惡意竄改時，系統將觸發資安阻斷防禦：

SqlException (Error 37346 / 37300)：底層偵測到雜湊鏈結斷裂時，服務會立即透過 ILogger 拋出 【核心安全警報】 級別日誌，拒絕後續資料存取，並回傳 IsSuccess = false 的失敗實體。

跨平台字型相容：報表引擎內建關閉嚴格字符檢查，並完整配置 Windows (Microsoft JhengHei) 與 Linux / Docker 容器環境 (Noto Sans TC) 的雙字型後備鏈，確保雲端部署不破圖。