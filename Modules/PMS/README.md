# SGS.Modules.PMS（專案管理外掛模組）

依《軟體專案開發與客戶維運 SOP 技術規範書》v3.0 與《開發技術規範》v1.0，把 PM 作業系統化。設計見 SDD v1.2、API 規格見
`G:\我的雲端硬碟\專案\PhysLIMS\PMS專案管理\`（計畫文件\SDD、規格文件）。

## 目前進度：M1 骨架（2026-11-13）

| 範圍 | 狀態 |
| --- | --- |
| 專案主檔、**專案代號核發**（格式／保留字／不可變／永不重用）、RACI 成員、模組目錄 Mxx | 完成 |
| 四道 SSDLC 卡控節點（Design→Build→Verification→Release 逐道放行，僅專案 Tech Lead）與階段推進 | 完成 |
| **專案文件（交付物）與 FMS 整合**：由 PMS 直接上傳（系統依 SOP 5.5 自動命名）、同版本唯一與最新版、浮水印檢視、授權下載（`PMS.DELIVERABLE.DOWNLOAD`）、虛擬刪除（PM／TL）、歸檔路徑對照 | 完成（2026-11-14，已對開發庫 API 實測） |
| 專案資料列授權（非成員一律 404，防 IDOR） | 完成 |
| 前端 RCL `FrontendFrameWork.RCL.Feature.ProjectManagement`：專案清單、專案詳情（六階段進度、成員／模組、Gate 決議、交付物）與 5 個對話框 | 完成（已編譯，尚未連後端實測） |
| FMS 附件分類 `pms-deliverable` / `pms-issue` / `pms-form`（`AllowGenericApi=false`） | 已加入 appsettings.json |
| 問題單（單號、分流、SLA、修復≠驗證 SoD、附件）與國定假日（含前端） | 完成（M2，2026-11-14，後端已實測；前端僅編譯） |
| CR、上線檢核表、RCA、UAT、月報、Intake、Stakeholder 帳號、排程通知 | **M2 後續／M3~M4，尚未實作** |

## 外掛模組結構

| 專案 | 內容 |
| --- | --- |
| `Module.PMS`（SGS.Modules.PMS） | `ProjectsController`、`DeliverablesController`（`api/v1/pms/...`）、`ModulePmsInitializer` |
| `Module.PMS.Application` | `Rules/`（純函式：`ProjectCodePolicy`、`GateRule`、`DeliverableNaming`、`ProjectAccessPolicy`）、`Services/ProjectService`、`Contracts/` |
| `Module.PMS.Infrastructure` | `PMSDbContext`（schema `pms`）、實體、設定、`PmsMigrationService`、Migrations（`PMSDB_Initial`） |

- plugins 目錄只部署 `SGS.Modules.PMS*.dll`（建置後自動複製到 Host 的 plugins）。
- 啟動時由 `ModulePmsInitializer` 以 MigrationConnection 套用 `pms` 遷移，並授予執行期帳號 `pms` DML。
  部署帳號無權授權時，請 DBA 先執行 `docs/sql/20261113_grant_pms_schema.sql`。
- 使用者僅存 `UserId`（Guid），不跨模組查表（ARC-01）；顯示名稱的解析（ORG 契約）留待 M2。

## API（M1）

| 方法 | 路徑 | 權限 |
| --- | --- | --- |
| POST | `api/v1/pms/projects/search` | `PMS.PROJECT.READ` |
| POST | `api/v1/pms/projects` | `PMS.PROJECT.MANAGE` |
| GET / PUT | `api/v1/pms/projects/{id}` | `PMS.PROJECT.READ` / `MANAGE` |
| PUT | `api/v1/pms/projects/{id}/phase` | `PMS.PROJECT.MANAGE` |
| GET | `api/v1/pms/projects/code/suggest`、`code/check` | `PMS.PROJECTCODE.MANAGE` |
| POST / DELETE | `api/v1/pms/projects/{id}/members[/{memberId}]` | `PMS.PROJECT.MANAGE` |
| POST | `api/v1/pms/projects/{id}/modules` | `PMS.PROJECT.MANAGE` |
| POST | `api/v1/pms/projects/{id}/gates/{gate}/decide` | `PMS.PROJECT.MANAGE`＋專案 Tech Lead |
| POST | `api/v1/pms/deliverables/naming-check` | `PMS.PROJECT.READ` |
| POST | `api/v1/pms/projects/{id}/deliverables/upload`（multipart，串流） | `PMS.DELIVERABLE.UPLOAD` |
| POST | `api/v1/pms/projects/{id}/deliverables/search` | `PMS.PROJECT.READ` |
| GET | `api/v1/pms/projects/{id}/deliverables/{deliverableId}/view`（浮水印） | `PMS.PROJECT.READ` |
| GET | `api/v1/pms/projects/{id}/deliverables/{deliverableId}/content`（原檔） | `PMS.DELIVERABLE.DOWNLOAD` |
| DELETE | `api/v1/pms/projects/{id}/deliverables/{deliverableId}` | `PMS.PROJECT.MANAGE`＋專案 PM／TL |

錯誤為 ProblemDetails，附 PMS 業務錯誤碼 `code`（例：`PMS.PROJECT_CODE_TAKEN`、`PMS.GATE_PREREQUISITE_NOT_MET`、`PMS.NAMING_RULE_VIOLATION`、`PMS.CONCURRENCY_CONFLICT`）。
選單由 `[Function(IsMenu, Path = "/sgs.modules.pms/projects")]` 自動種子；權限碼由 `[RequiresPermission]` 同步，需於角色管理授予。

## 前端（M1）

| 路由 | 頁面 |
| --- | --- |
| `/sgs.modules.pms/projects` | 專案清單：搜尋、期別／階段篩選、分頁、建立專案（代號檢查與建議） |
| `/sgs.modules.pms/projects/{id}` | 專案詳情：六階段進度與推進、成員／模組、SSDLC 卡控節點決議、交付物登記與查詢（命名即時檢查） |

- 已註冊於 `BlazorWasmApp`（`AddProjectManagementFeature()`、`App.razor.cs` 路由組件、專案參考）。
- 選單由後端 `[Function(IsMenu, Path)]` 種子，需於角色管理授予 `PMS.*` 權限後才會出現。
- 使用者欄位暫以 Guid 輸入與顯示縮寫（M2 接 ORG 使用者選擇器與名稱解析）。
- Host 未全域註冊 `JsonStringEnumConverter`，PMS 列舉以屬性標註為字串（`EnumJsonTests` 守護）。

## M2 問題單（2026-11-14）

依 SOP 6 與附錄 B-3：客戶問題單全流程、SLA、假日與附件。CR 變更單（FORM-CR-01）留待下一輪；分流為「需求變更」時僅記錄類型（不計 SLA、可由 PM 直接結案），尚未自動建立 CR 單。

| 範圍 | 說明 |
| --- | --- |
| 單號 | `專案代號-流水號`（至少 3 位，例 `PRJ-001`）；`pms.TicketSequences` 樂觀並行遞增，衝突自動重試（最多 5 次） |
| 狀態機 | 受理 → 分流 → 處理中 → 待驗證 → 待複測 → 結案；可取消（受理／處理中）、客戶複測未通過可重開；諮詢／資料撈取／CR 可由 PM 於處理中直接結案（`TicketWorkflow`） |
| 角色 | 分流、結案、重開、取消＝專案 PM；指派＝Tech Lead；回報修復＝被指派修復人；驗證＝被指派驗證人 |
| SoD | 修復人≠驗證人（`TicketSeparationOfDuties`，服務層＋資料表 CHECK `CK_Tickets_FixerNotVerifier`）；二者須為專案 BE／FE／Tech Lead；修復須附重現測試（Bug 驅動測試） |
| SLA | `SlaCalculator`：P1 受理＋4 小時、P2 1 工作天、P3 2 工作天、諮詢／資料撈取 1 工作天、CR 不計；工作日以台灣時間（UTC+8）判定，扣除 `pms.Holidays`（放假／補班）；燈號於讀取時動態計算（進行中／即將到期 <2h／已超時／準時達成／超時完成），不持久化 |
| 資料列授權 | 非成員一律 404；Stakeholder 僅見並建立「客戶可見且自己提出」的單，且看不到事件時間軸與修復／驗證人 |
| 附件 | FMS 分類 `pms-issue`（`AllowGenericApi=false`，20MB，Office／PDF／圖片／txt／csv）；檔名 `YYYYMMDD_[單號]_檔案說明.ext`；上傳後為「待遮罩確認」（SOP 8-4）；浮水印檢視、下載、刪除（PM／TL） |
| 假日 | `PMS.SETTINGS.MANAGE` 維護國定假日與補班日；修改不會回頭重算已分流問題單的期限 |

API（`api/v1/pms`）：`tickets/search`、`tickets`（建單）、`tickets/{id}`、`/triage`、`/assign`、`/resolve`、`/verify`、`/close`、`/reopen`、`/cancel`、`/attachments`（上傳）、`/attachments/{aid}/view|content|mask-confirm`（刪除為 `DELETE`）；`holidays/search`、`PUT holidays`、`DELETE holidays/{yyyy-MM-dd}`。
權限：`PMS.TICKET.READ／CREATE／TRIAGE／RESOLVE／VERIFY`（TRIAGE 涵蓋 PM 分流與結案、Tech Lead 指派）、`PMS.SETTINGS.MANAGE`。

前端：`/sgs.modules.pms/tickets`（清單：SLA 摘要與燈號、篩選）、`/tickets/{id}`（詳情：SLA、人員、附件、時間軸，依角色顯示動作按鈕）、`/holidays`（假日維護）。

## CR 需求變更單（2026-11-14，SOP 第 4 章 / FORM-CR-01）

- **流程**：PM 建立（或問題單分流為「需求變更」時自動建立）→ Tech Lead 於 2 個工作天內影響評估（層級、API 契約、耦合度、資安、BE/FE/驗證工時）→ PM 決策（A 時程順延／B 範圍交換／C 駁回或暫緩）→ A、B 進入簽核（PM＋Tech Lead，客戶 CR 加客戶）→ 全數簽核後核准。
- **暫緩後續**：決策 C 暫緩（轉 Backlog）的 CR，下期規劃時由專案 PM 按「重啟 CR」（`POST {id}/reopen`，`PMS.CR.CREATE`）回到受理，重新起算 2 工作天評估期限並清除舊評估與決策（時間軸留紀錄）；駁回與取消為終態。
- **內容鎖定**：決策時以 SHA-256 固化 CR 內容；簽核須帶相同雜湊，內容不一致即拒絕。客戶簽核目前由 PM 代錄（附原因與依據），待 Stakeholder 外部帳號開放後改為自行簽核。
- **API**（`api/v1/pms/change-requests`）：`POST search`、`POST`（建立）、`GET {id}`、`POST {id}/evaluate`、`/decide`、`/sign`、`/customer-sign/proxy`、`/cancel`；狀態轉換皆帶 `rowVersion`。
- **權限碼**：`PMS.CR.READ`、`PMS.CR.CREATE`、`PMS.CR.EVALUATE`、`PMS.CR.APPROVE`。誰能執行由專案角色判斷（建立／決策／代錄＝PM；評估＝Tech Lead）。建議「專案管理使用者」加 `PMS.CR.READ`、`PMS.CR.EVALUATE`；「專案管理員」加 `PMS.CR.CREATE`、`PMS.CR.APPROVE`。
- **評估逾期提醒（`pms-cr-eval-watch`）**：SCH 作業平日 09:00 呼叫 `POST api/v1/pms/change-requests/scan-overdue`（scope `pms.cr.scan`，亦可由具 `PMS.SETTINGS.MANAGE` 者手動執行）。受理後超過 2 個工作天仍未評估的 CR，依專案彙整成一封 Email 寄給該專案 Tech Lead 與 PM（需使用者帳號有 Email，且系統告警 SMTP 已設定）；同一張 CR 每個台灣日最多一次，週末與國定假日不發。作業僅在 `ProjectManagement:Enabled=true` 時註冊（預設 false，開發環境 true）。
- **遷移**：`PMSDB_ChangeRequests`、`PMSDB_CrEvalReminder`（新增 `LastEvalReminderAt`，啟動 API 時自動套用）。
- **前端**：`/sgs.modules.pms/change-requests`（清單）、`/change-requests/{id}`（詳情與動作）。
- **尚未驗證**：僅完成單元測試（245 通過）與建置；未做 PM／Tech Lead 雙帳號的實機流程。

尚未實作（後續）：SLA 到期通知與排程（`pms-sla-watch`）、RCA 表單、LINE／Email 收單、Stakeholder 外部帳號。

## 角色與權限配置

**兩層授權，各管不同的事**

| 層次 | 在哪裡設定 | 決定什麼 | 範圍 |
| --- | --- | --- | --- |
| 功能權限 | 系統「角色管理」（權限碼 `PMS.*`） | 能不能使用某功能（選單是否出現、API 是否回 403） | 全系統，與專案無關 |
| 專案角色（RACI） | 專案詳情的「專案成員」（PM／Tech Lead／BE／FE） | 在**這一個專案**裡是誰；分流、指派、修復、驗證、放行 Gate 以此判斷，非成員一律 404 | 單一專案 |

同一個人可以在 A 專案是 PM、在 B 專案是 BE；專案角色不會自動帶來功能權限，功能權限也不代表能操作某個專案。

**採精簡做法：只建兩個系統角色**，不為 PM／Tech Lead／BE／FE 各建一個角色。「誰能做哪個動作」交由專案成員的 RACI 角色決定，不在角色管理重複維護。

| 系統角色 | 授予對象 | 權限碼 |
| --- | --- | --- |
| 專案管理使用者 | 所有專案成員（PM、Tech Lead、BE、FE） | `PMS.PROJECT.READ`、`PMS.DELIVERABLE.UPLOAD`、`PMS.TICKET.READ`、`PMS.TICKET.CREATE`、`PMS.TICKET.TRIAGE`、`PMS.TICKET.RESOLVE`、`PMS.TICKET.VERIFY`、`PMS.CR.READ`、`PMS.CR.EVALUATE`、`PMS.RELEASE.READ`、`PMS.RELEASE.CHECK`、`PMS.RELEASE.APPROVE`、`PMS.RCA.WRITE`、`PMS.UAT.READ`、`PMS.UAT.CREATE`、`PMS.REPORT.READ`、`PMS.REPORT.GENERATE`、`PMS.RISK.READ`、`PMS.RISK.MANAGE`、`PMS.GANTT.READ`、`PMS.GANTT.MANAGE` |
| 專案管理員 | PM、Tech Lead（包含上列所有權限，再加下列） | `PMS.PROJECT.MANAGE`、`PMS.PROJECTCODE.MANAGE`、`PMS.SETTINGS.READ`、`PMS.SETTINGS.MANAGE`、`PMS.DELIVERABLE.DOWNLOAD`、`PMS.CR.CREATE`、`PMS.CR.APPROVE` |

- PM、Tech Lead 只掛「專案管理員」即可（它已包含使用者角色的權限）；BE、FE 只掛「專案管理使用者」。
- `TRIAGE`、`RESOLVE`、`VERIFY` 在伺服器端都會再以專案角色檢查（分流／結案／重開／取消＝專案 PM；指派＝Tech Lead；回報修復＝被指派修復人；驗證＝被指派驗證人且不得為修復人），因此授予全體成員不會讓人越權。
- 外部 Stakeholder 不使用這兩個角色；其簽核帳號機制另案處理。

**已知限制（使用前請知悉）**
1. **上傳與管理已分離**：上傳交付物使用獨立的 `PMS.DELIVERABLE.UPLOAD`（含在「專案管理使用者」），BE／FE 可上傳專案文件而不需要 `PMS.PROJECT.MANAGE`。刪除交付物仍須 `PMS.PROJECT.MANAGE` 且為專案 PM／Tech Lead。`PMS.PROJECT.MANAGE` 涵蓋建立／更新專案、新增成員與模組，請只授予 PM／Tech Lead。
2. `PMS.DELIVERABLE.DOWNLOAD` 預設只給「專案管理員」；若 BE／FE 需要下載原檔，請另行加到「使用者」角色。檢視浮水印內容只需 `PMS.PROJECT.READ`。
3. 權限碼由 API 啟動時同步到「系統權限清單」；看不到 `PMS.*` 時，請於權限清單頁按「重新同步權限元數據並更新權限樹」。
4. 角色異動須重新登入才會生效；新建角色可能需經審核後啟用，且治理設定（Maker-Checker、職責分離）可能限制同一人可掛的角色，遇到擋下請依訊息處理。
5. **「國定假日」選單**由 `PMS.SETTINGS.READ` 控制（僅「專案管理員」看得到）；選單頁面節點以權限碼為 Key，每個選單入口必須使用各自獨立的權限碼，開發新選單入口時請勿與其他入口共用同一個權限碼（否則兩個入口會互相覆蓋，其中一個選單會消失）。

## 上線檢核表（2026-11-14，SOP 附錄 A / FORM-SEC-01）

- **流程**：內部成員建立（版本、預計上線日、本次主要作者）→ BE／FE 逐項檢核 SEC-01~11、REL-01~06 → 交叉驗證簽核（不得為主要作者）→ Tech Lead 放行（阻斷項須全數通過／不適用並說明；警告項未完成須附例外備註；**同時通過專案 Release Gate**，需 Verification Gate 已通過）→ PM 確認。放行後內容鎖定。
- **API**（`api/v1/pms/release-checklists`）：`POST search`、`POST`（建立）、`GET {id}`、`PUT {id}/items/{code}`、`POST {id}/signoff`；皆帶 `rowVersion`。
- **權限碼**：`PMS.RELEASE.READ`（查詢／詳情；選單入口）、`PMS.RELEASE.CHECK`（建立、逐項檢核）、`PMS.RELEASE.APPROVE`（簽核）。三者建議都放在「專案管理使用者」角色（全體成員），實際誰能簽哪個角色由專案成員角色（RACI）判斷。
- **遷移**：`PMSDB_ReleaseChecklists`（新增 ReleaseChecklists／ReleaseChecklistItems／ReleaseSignoffs，啟動 API 時自動套用）。
- **前端**：`/sgs.modules.pms/release-checklists`（清單）、`/release-checklists/{id}`（逐項檢核、簽核）。
- **尚未實作**：證據檔（待 FMS `pms-form` 整合；目前於備註填報告歸檔路徑）。
- **尚未驗證**：僅完成單元測試（286 通過）與建置；未做實機流程驗證。

## RCA、UAT 驗收簽核、月度報告（2026-11-14）

- **RCA**（`api/v1/pms/rca/{ticketId}`）：Tech Lead 撰寫並於系統恢復後 48 小時內提交，PM 潤飾後記錄寄送客戶；觸發條件須符合（P1／SLA 超時／降級回滾）；內文疑似點名個人僅警示。入口：問題單詳情的「RCA 根因分析」按鈕（頁面 `/sgs.modules.pms/rca/{ticketId}`）。權限 `PMS.RCA.WRITE`。**PDF 與代寄（v2.5）**：RCA 頁可「預覽 PDF」（未寄送前標示內部預覽）；PM 於「寄送客戶」可選**系統代寄**（產生正式版 PDF 存 FMS，以 Email 附件寄給填寫的客戶收件人，成功後才標記已寄送，Reply-To 為寄件 PM）或**僅記錄**（自行寄出）。代寄需設定 SMTP：`AlertSettings:Smtp`（`Host`、`Port`、`EnableSsl`、`FromAddress`、`Username`／`Password`；帳密放 `claudesettings.json` 或環境變數，不入版控），未設定時對話框自動停用代寄。遷移 `PMSDB_RcaSend`。
- **UAT 驗收簽核**（`api/v1/pms/uat-signoffs`）：PM 建立（版本＋驗收範圍，內容雜湊固化）→ 客戶驗收通過／不通過，客戶外部帳號開放前由 PM 代錄並附依據。選單「UAT 驗收簽核」，權限 `PMS.UAT.READ`、`PMS.UAT.CREATE`。
- **UAT 驗收附件**（`api/v1/pms/uat-signoffs/{id}/attachments`）：可上傳多個客戶回覆信件／簽署掃描檔／驗收截圖（前端多選逐一上傳）；FMS `pms-issue`（加密）、系統命名 `YYYYMMDD_專案代號_UAT_版本_說明`、每張最多 50 個、上傳後待遮罩確認；上傳／確認遮罩／刪除限 PM／Tech Lead，驗收結果記錄後鎖定刪除。沿用 `PMS.UAT.READ`／`PMS.UAT.CREATE`，不需新權限。遷移 `PMSDB_UatAttachments`。
- **月度儀表板與月報**（`api/v1/pms/dashboard/monthly`、`reports/monthly/*`）：統計總進線單數、SLA 準時達成率、MTTR、類型佔比、逾期與即將到期；PM／Tech Lead 產生 PDF（QuestPDF，Noto Sans TC）存 FMS `pms-form`（加密，僅 `.pdf`），內部成員下載。選單「月度維運報告」，權限 `PMS.REPORT.READ`、`PMS.REPORT.GENERATE`。
- **月報排程自動產生**：SCH 作業 `pms-report-monthly` 每天 18:00 呼叫 `POST api/v1/pms/reports/monthly/auto-generate`（scope `pms.report.monthly`；具 `PMS.SETTINGS.MANAGE` 者可手動執行並以 `?period=YYYY-MM` 回補）。月底最後一個工作日產生當月、月初前 3 個工作日補產上月缺漏；只處理進行中且維運期的專案；已有月報不覆蓋；產生後 Email 通知該專案 PM／Tech Lead（需有 Email 且系統告警 SMTP 已設定）。作業僅在 `ProjectManagement:Enabled=true` 時註冊。
- **專案風險表**（`api/v1/pms/risks`）：PM／Tech Lead 新增與維護風險（標題、等級、狀態、因應措施、負責人），風險負責人更新自己風險的狀態與因應措施；結案自動記錄時間；作廢為軟刪除；僅專案內部成員可見，客戶看不到。選單「風險表」，權限 `PMS.RISK.READ`、`PMS.RISK.MANAGE`；遷移 `PMSDB_RiskItems`。
- **專案甘特圖**（`api/v1/pms/projects/{id}/gantt`，頁面 `/sgs.modules.pms/gantt`，專案詳情有「甘特圖」入口）：WBS 任務（階層、起迄、負責人、進度、里程碑）、前置相依（完成後才開始，可設落後天數；循環偵測；違反時標示衝突）、基準線與偏差（延後／提前天數）、圖上拖曳移動／縮放（可連動順延後繼任務）、自動疊加凍結線／Gate／上線／CR 順延／UAT 驗收里程碑、一鍵套用六階段預設計畫。PM／Tech Lead 維護，被指派者更新自己任務進度，客戶看不到。日期為日曆日（不扣週末與假日）。**匯出**：工具列「匯出 WBS」（Excel）與「匯出甘特圖」（PDF，A4 橫式，多頁，含相依線／基準線／里程碑），沿用 `PMS.GANTT.READ`。權限 `PMS.GANTT.READ`、`PMS.GANTT.MANAGE`；遷移 `PMSDB_Gantt`。
- **專案合約管理（第一期）**（`api/v1/pms/contracts`，頁面 `/sgs.modules.pms/contracts`）：依《資訊系統專案合約管理規範與指引（乙方標準版）》——合約主檔與條款參數、審約檢核表（依條款自動比對乙方底線並標示偏離）、PM→法務→高階主管核決（職責分離、送審快照鎖定）、簽約登錄、附件一《交付項目清單》與附件二《SLA 分級》、簽約後綁定 1~N 個 PMS 專案、合約附件（FMS `pms-contract`，檢視加浮水印）。僅合約成員可見，客戶看不到。權限 `PMS.CONTRACT.READ`／`MANAGE`／`REVIEW`／`APPROVE` **請依職責個別授權（業務、PM、法務、管理層），不屬「專案管理使用者」預設角色**；遷移 `PMSDB_Contracts`。第二期（協力遲延通知書、審查期限與擬制驗收、合約變更、付款）與第三期（保固維運、產業合規、儀表與匯出）規劃中。
- **權限配置**：以上權限建議全部放在「專案管理使用者」角色（`PMS.CONTRACT.*` 除外，見上）；實際誰能做哪個動作由專案成員角色（RACI）判斷。
- **遷移**：`PMSDB_RcaReports`、`PMSDB_UatSignoffs`、`PMSDB_MonthlyReports`（啟動 API 時自動套用）。
- **尚未驗證**：僅完成單元測試（503 通過）與建置；未實機驗證（特別是月報 PDF 經 FMS 存取、RCA 與 UAT 流程）。需重啟 API 並確認 FMS 已啟用（`pms-form` 分類已改為僅允許 `.pdf`）。
- **尚未實作**：UAT 客戶自行簽核（待 Stakeholder 外部簽核帳號）。

## 專案文件 × FMS 整合（2026-11-14）

- 檔案走 FMS 分類 `pms-deliverable`（`AllowGenericApi=false`），**只能經 PMS 授權後存取**（`FileAccessScope.Internal`，同 RPT 模式）；PMS 不接受外部提供的 FMS 檔案識別碼，檔案只能經「上傳」建立交付物。
- 檔名由系統產生：`YYYYMMDD_文件名稱_版本_上傳者.ext`（模組文件含 `Mxx_模組名稱`），原始檔名另存；同專案＋模組＋文件類型的同版本唯一（刪除後可重傳）。
- 允許格式：pdf、docx、xlsx、pptx、doc、xls、ppt、png、jpg、jpeg、gif、txt、csv（FMS 簽章檢查支援者；`.md`／`.yaml`／`.json`／壓縮檔不在此列）；單檔 50MB。
- DB 寫入失敗時補償回收已寫入 FMS 的檔案。舊版（M1）僅登記索引的交付物 `FileSize` 為 NULL，UI 標示「無實體檔案」。
- 前端檢視重用 FMS RCL 的 `FileViewerDialog`（新增選用 `Loader` 委派），下載重用 `fileManagement.js`；因此 PMS RCL 參考了 FileManagement RCL。
- 需於角色管理授予 `PMS.DELIVERABLE.DOWNLOAD` 才能下載原檔（檢視浮水印內容僅需 `PMS.PROJECT.READ`）。

## 與規格的已知差異（M1）

- 規格中的 `displayName`（成員、PM 姓名）M1 尚未回傳，僅 `userId`。
- 外部 Stakeholder 邀請端點（2.1.10）尚未實作，待 Identity 外部使用者機制確認（M3）。

## 測試

```
dotnet test Tests/SGS.Modules.PMS.Tests -c Release
```
目前 191 個單元測試（專案代號、Gate 規則、命名鐵律與自動命名、資料列授權、SLA 計算、問題單狀態機與 SoD、列舉 JSON）。`ProjectService` 的資料庫整合測試待 Testcontainers（M2）。
