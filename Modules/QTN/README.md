# SGS.Modules.QTN — 報價管理外掛模組

依《SOP-PMO-2026-001 資訊軟體專案承攬報價管理規範與執行指引 v2.0》實作的企業報價管理模組：商機立案 → WBS 三點估時 → 成本核算與定價 → 毛利紅綠燈多層級審批 → 對外報價與轉 PMS 合約 → CR 變更 → 結案復盤。

## 四大治理原則的落地

| 原則 | 實作 |
|---|---|
| 技術評估前置 | 送審前須由商機 Tech Lead 執行「技術複核」；WBS 或計價期數變動後自動清除複核 |
| 數據快照 | `WbsRoleAllocations.SnapshotCostRate／SnapshotListRate` 於配置時寫入；調整角色費率只留歷程，不回溯既有報價；衍生新版本可選「沿用快照」或「改用目前費率」 |
| 版本不可變 | 僅「未鎖定的草稿」可編修；送審即鎖定並凍結 SHA-256 內容雜湊，審批時比對；調整須衍生 v1.1、v1.2-CR 新版本，已核准／已發出的來源轉為 Superseded |
| 交付基準對齊 | 已成交主報價一鍵經 Core `IQuoteContractIntegration` 建立 PMS 合約（報價評估狀態），帶入總價、付款里程碑與 WBS 執行基準（冪等） |

## 架構

- `Module.QTN`：初始化器、控制器（`api/v1/qtn/*`）
- `Module.QTN.Application`：`Rules/`（純函式，皆有單元測試）、`Services/`、`Contracts/`
- `Module.QTN.Infrastructure`：`QTNDbContext`（schema `qtn`）、實體、遷移 `QTNDB_Initial`
- Core 契約：`SGSFramework.Core.ProjectContracts.IQuoteContractIntegration`（預設 Null → 503；PMS 載入時覆寫為 `QuoteContractIntegration`）
- 前端：`FrontendFrameWork.RCL.Feature.Quotation`（路由 `/sgs.modules.qtn/*`）
- 排程：SCH `qtn-quote-expiry`、`qtn-cr-overdue`、`qtn-variance-due`（需 `Quotation:Enabled=true`）

## 核心規則（`Application/Rules`）

- **PERT**：E＝(O＋4M＋P)÷6；報價人天＝E × K技術（1.0／1.2／1.5）× K需求（1.0／1.3／1.6）
- **結構比例**：分析 15–20%、開發 50–60%、測試 15–20%、管理 10–15%；固定總價／混合計價缺任一支援階段不得送審，超出區間僅提示
- **成本**：總成本＝Σ(人天×成本快照)＋直接費用＋管銷分攤（人力成本×18%）；毛利率＝(成交價未稅－總成本)÷成交價未稅
- **期數**：T&M 的 WBS 為單一 Sprint、Retainer 為單月，人力與直接費用乘以 Sprint 數／月數
- **IP 買斷**：授權費不得低於牌價（人力＋直接費用）30%，不足自動補足；強制法務長會簽
- **燈號與路徑**（取最高層級）：綠（<100 萬且 ≥40%）TL→業務經理；黃（100–500 萬或 30–39%）＋PMO 總監；紅（>500 萬或 <30%）＋財務長→總經理；<20% 鎖定送出（戰略客戶特批除外）；獎金係數 1.0／0.8／0.5；黃燈以上須填折扣說明
- **職責分離**：送審者不得審批；同一人同輪只能決定一關
- **付款里程碑**：固定總價預設 30/20/30/20，尾差歸末期；T&M 依 Sprint、月費依月、混合計價依兩階段
- **有效期**：出具日起 30 個日曆天；排程每日轉逾期並提醒 7 日內到期
- **CR**：收件後 3 個工作日內評估（依 CAL 假日）；以新增／重工／回歸／管理工時衍生 vX.Y-CR 報價，經審批與客戶簽署後成交
- **復盤**：結案後 14 個工作日內；偏差 >±15% 須記原因並於 7 日內校準標準工時庫（最可能值＝實際人天，O／P 等比例，基準版本 +1）

## 欄位級權限（SOP 6.2）

後端依權限投影 DTO（前端不參與判斷）：

| 權限 | 可見 |
|---|---|
| `QTN.COST.READ` | 全部（成本單價、總成本、毛利率、毛利率報告 PDF） |
| `QTN.PRICE.READ` | 牌價、成交價、燈號、對外報價單 PDF；成本與毛利率為 null |
| 皆無（Tech Lead／PM） | 只有工時與係數；所有金額、毛利與燈號遮罩 |

資料列授權：商機成員（業務／Tech Lead／PM／財務）可見；審批權責矩陣中被指派者可見全部；其餘 404。

## 權限碼（於「角色管理」授與）

| 權限碼 | 用途 |
|---|---|
| `QTN.DASHBOARD.READ` | 報價儀表板（選單） |
| `QTN.OPPORTUNITY.READ`／`.MANAGE` | 商機（選單）／建立、修改、成員 |
| `QTN.QUOTE.READ` | 報價單（選單）、詳情、編修入口、衍生版本、附件、PDF |
| `QTN.QUOTE.EDIT` | 建立報價、定價與條款編修、送審、發出、成交、建立合約 |
| `QTN.ESTIMATE.EDIT` | WBS 與三點工時、匯入模組、技術複核、CR 評估 |
| `QTN.QUOTE.APPROVE` | 待我審批（選單）、核准／退件 |
| `QTN.PRICE.READ`／`QTN.COST.READ` | 欄位級權限（見上表） |
| `QTN.CR.READ`／`.MANAGE` | CR 變更評估（選單）／維護 |
| `QTN.REVIEW.READ`／`.MANAGE` | 結案工時復盤（選單）／維護 |
| `QTN.CUSTOMER.READ`／`.MANAGE` | 報價客戶（選單）／維護 |
| `QTN.RATE.READ`／`.MANAGE` | 角色費率（選單）／維護（另需 COST.READ） |
| `QTN.TEMPLATE.READ`／`.MANAGE` | 標準 WBS 工時庫（選單）／維護、復盤校準 |
| `QTN.APPROVER.MANAGE` | 報價審批人設定（選單） |
| `QTN.SCHEDULE.RUN` | 排程掃描端點（系統帳號另需 scope） |

建議角色：

- **業務**：OPPORTUNITY.*、QUOTE.READ/EDIT、PRICE.READ、CR.READ、CUSTOMER.*、DASHBOARD.READ
- **Tech Lead／PM**：OPPORTUNITY.READ、QUOTE.READ、ESTIMATE.EDIT、CR.*、REVIEW.*、TEMPLATE.*、DASHBOARD.READ
- **財務／主管**：上述讀取＋QUOTE.APPROVE、COST.READ、RATE.*

## 部署

1. 將 `SGS.Modules.QTN*.dll` 放入 `plugins`（建置時自動複製）；首次啟動自動建立 `qtn` schema 與資料表。部署帳號無授權權限時，請 DBA 執行 `docs/sql/20261116_grant_qtn_schema.sql`（含 ApprovalLogs 只增不改的 DENY）。
2. `appsettings.json`：`Quotation:Enabled=true` 註冊排程；`FileManagement:Categories:qtn-quotation` 已預設。
3. 系統帳號管理：排程帳號授與 scope `qtn.quote.scan`、`qtn.cr.scan`、`qtn.review.scan`。
4. 依序設定：角色費率 → 標準 WBS 工時庫 → 報價審批人 → 客戶。
5. 一鍵建立合約需同時部署 PMS 模組（否則回應 503）。

## 測試與驗證

### 一、自動化測試

```bash
dotnet test Tests/SGS.Modules.QTN.Tests -c Release
```

單元測試僅涵蓋 `Application/Rules` 的純函式與 `QuoteCalculator`，不連資料庫（2026-11-16 結果：90 項全數通過）。另應一併回歸受影響的模組：

```bash
dotnet test Tests/SGS.Modules.PMS.Tests -c Release
dotnet test Tests/SGS.Modules.SCH.Tests -c Release
dotnet test Tests/SGSFramework.Core.Tests -c Release
```

| 測試類別 | 驗證重點 |
|---|---|
| `PertCalculatorTests` | E＝(O＋4M＋P)÷6、K 係數只接受標準值、只在最後四捨五入一次、三點工時檢核 |
| `PricingCalculatorTests` | 範本一（牌價 1,266,000 → 成交 1,200,000、含稅 1,260,000）、管銷 18%、範本二 Sprint 產能包（575,000 × 3）、IP 授權費 30% 下限、折扣上限 |
| `MilestoneRulesTests` | 30/20/30/20 含稅金額 378k／252k／378k／252k、尾差歸末期、各計價模式預設期別 |
| `PhaseRatioRulesTests` | 支援階段缺漏阻擋送審、比例超出區間只提示 |
| `MarginLightPolicyTests` | 燈號邊界（100 萬、500 萬、40%、30%）、審批路徑、IP 法務會簽、<20% 鎖定與特批、獎金係數 |
| `ApprovalSodTests` | 送審者不得審批、同人同輪只能決定一關 |
| `QuoteWorkflowTests` | 僅未鎖定草稿可編修、送審需技術複核、可衍生版本的來源狀態、版本號、有效期與逾期 |
| `VarianceAndWorkdayTests` | ±15% 偏差門檻、校準比例、工作日（含假日）計算 |
| `FieldSecurityPolicyTests` | 三種欄位權限的金額遮罩 |
| `QuoteCalculatorTests` | WBS 編號、重算回寫總額與里程碑、內容雜湊隨範圍變動 |
| `EnumJsonTests` | 列舉以字串序列化（Host 未註冊全域轉換器） |

### 二、部署驗證（啟動 Host 後）

1. **遷移**：啟動日誌出現「開始執行 SGS.Modules.QTN 模組資料庫自動遷移 (Schema: qtn)」與「遷移作業成功完成」；資料庫有 `qtn` schema 及 22 張資料表，`[qtn].[__EFMigrationsHistory]` 有 `QTNDB_Initial`。權限不足時先由 DBA 執行 `docs/sql/20261116_grant_qtn_schema.sql`。
2. **選單**：「報價管理」下出現 10 個選單（儀表板、商機、報價單、待我審批、CR 變更評估、結案工時復盤、客戶、角色費率、標準 WBS 工時庫、審批人設定）。
3. **權限碼**：「角色管理」可授與全部 `QTN.*` 權限碼（含欄位權限 `QTN.COST.READ`、`QTN.PRICE.READ`）。

### 三、端到端測試情境

先準備三個測試帳號並授與建議角色：**業務 A**、**Tech Lead B**、**財務／主管 C**；再將 C 指派為業務經理、PMO 總監、財務長、總經理、法務長以外的必要關卡人選，或另備帳號分別指派（同一人同輪只能決定一關）。

| # | 情境 | 操作 | 預期結果 |
|---|---|---|---|
| 1 | 主檔準備 | C 建立角色費率（SA、BE、QA、PM）→ 標準 WBS 模組 → 審批人 → 客戶 | 費率調整須填原因並出現在歷程 |
| 2 | 商機立案 | A 建立商機（固定總價），成員加入 B 為 Tech Lead | A 自動成為業務；畫面顯示策略建議 |
| 3 | 建立報價 | A 勾選標準模組建立 v1.0 | 單號 `QT-FP-YYYYMMDD-01`；WBS 已帶入；預設 4 期里程碑與附錄 A 條款 |
| 4 | 估時 | B 調整三點工時與 K 係數 → 試算 → 儲存 WBS → 技術複核 | 試算不寫入；儲存後技術複核標記清除，需重新複核 |
| 5 | 技術評估前置 | 未複核時 A 嘗試送審 | 「送審」按鈕停用，並列出阻擋原因 |
| 6 | 欄位遮罩 | A、B、C 分別開啟同一張報價 | B 看不到任何金額與燈號；A 看得到牌價、成交價、燈號，看不到成本與毛利率；C 全部可見 |
| 7 | 綠燈審批 | 成交價 < 100 萬且毛利 ≥ 40%，A 送審 | 路徑 Tech Lead → 業務經理；報價鎖定；第一關審批人收到通知信 |
| 8 | 職責分離 | A 嘗試審批自己送審的報價；同一人連續決定兩關 | 皆被拒絕並顯示原因 |
| 9 | 黃／紅燈 | 調整折扣使金額 ≥ 100 萬或毛利 < 40%（另測 > 500 萬或 < 30%） | 黃燈加 PMO 總監並要求折扣說明；紅燈加財務長 → 總經理 |
| 10 | 毛利鎖定 | 毛利 < 20%，一般客戶與戰略客戶各送審一次 | 一般客戶被鎖定；戰略客戶勾選特批並填理由後可送審 |
| 11 | IP 買斷 | 商機勾選 IP 買斷後建立報價 | IP 授權費自動補足牌價 30%；路徑末端加法務長 |
| 12 | 退件與衍生 | 審批人退件 → 衍生新版本 | 退件版本保持「已退件」；新版本 v1.1 為草稿，需重新技術複核 |
| 13 | 版本取代 | 已核准版本衍生新版本（勾選／不勾選「改用目前費率」） | 來源轉為「已取代」；不勾選時沿用原費率快照 |
| 14 | 發出與成交 | A 對外發出 → 登錄成交 | 有效期為出具日 + 30 天；商機轉為已成交 |
| 15 | PDF | 匯出報價單、工時估算表、毛利率報告 | 報價單不含成本；工時估算表無金額；毛利率報告僅 C 可匯出；稽核軌跡記錄匯出 |
| 16 | 轉 PMS 合約 | A 按「建立 PMS 合約」，再按一次 | 建立「報價評估」狀態合約，備註含里程碑與 WBS 基準；重按不會重複建立 |
| 17 | CR | 於成交報價「提出 CR」→ 填評估工時 → 衍生 CR 報價 → 送審核准 → 登錄客戶簽署 | 評估期限為收件後 3 個工作日（含假日）；產生 v1.x-CR；簽署後 CR 報價轉成交 |
| 18 | 結案復盤 | 建立復盤 → 填實際人天（製造 > 15% 偏差）→ 填原因與結論 → 校準 | 超標項目標紅；校準後標準模組三點工時更新、基準版本 +1；復盤結案 |
| 19 | 附件 | 上傳折扣說明書、客戶簽回文件，嘗試刪除 | 客戶簽回文件不可刪除；非成員無法存取（404） |
| 20 | 資料列授權 | 以非成員且非審批人的帳號開啟報價網址 | 回應 404 |
| 21 | 防竄改 | 送審後直接以 SQL 修改該報價 WBS 工時，再審批 | 審批被拒絕（QTN.CONTENT_TAMPERED） |
| 22 | 排程 | 設 `Quotation:Enabled=true`，於排程管理手動觸發 `qtn-quote-expiry` | 已發出且逾期的報價轉為逾期；7 日內到期者收到提醒 |

### 四、測試注意事項

- 審批人通知信需系統告警 SMTP 已啟用；未啟用時流程照常，只是不寄信。
- 附件需部署 FMS 模組，否則回應 503；建立合約需部署 PMS 模組，否則回應 503。
- 情境 21 會改動資料，請只在測試資料庫進行。

## 已知限制

- PMS 合約的付款期程須簽約後設定（PMS 規則），因此里程碑與 WBS 執行基準寫入合約備註，由 PM 簽約後依備註設定付款期程並建立專案基準。
- 結案復盤的實際人天目前為手動填寫（PMS 尚無工時填報資料可匯入）。
- 對外報價單 PDF 抬頭為通用標題，尚未接系統配置的公司名稱。
