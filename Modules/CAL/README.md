# SGS.Modules.CAL — 行事曆管理外掛模組

公司工作日曆（假日與補班日）的**唯一來源**，並提供全公司行事曆（月檢視與活動維護）。PMS 的 SLA、CR 評估、合約工作日、加班判定、月報等經 Core 的 `IWorkCalendarProvider` 讀取假日，不直接參照本模組。

## 專案結構
`Module.CAL`（控制器、`ModuleCalInitializer`）／`Module.CAL.Application`（規則、服務、契約、DI）／`Module.CAL.Infrastructure`（`CALDbContext`、實體、遷移、`CalMigrationService`）；測試 `Tests/SGS.Modules.CAL.Tests`。樣板同 SCH／PMS（strong-name 簽章、建置後複製到 Host `plugins`）。

## 資料（schema `cal`）
`Holidays`（日期 PK）、`Calendars`（全公司日曆固定 Id 由遷移種子建立）、`CalendarEvents`。初始遷移會一次性把 `pms.Holidays` 複製到 `cal.Holidays`（只在 pms 表存在時、已存在日期不覆蓋）。DBA 授權腳本：`docs/sql/20261115_grant_cal_schema.sql`。

## API 與權限
- `POST api/v1/cal/calendar/search`（`CAL.CALENDAR.READ`，選單「公司行事曆」）；活動新增、修改、刪除 `CAL.EVENT.MANAGE`。
- `POST api/v1/cal/holidays/search`（`CAL.HOLIDAY.READ`，選單「假日與補班」）；`PUT`、`DELETE {date}`、`POST import`（CSV 預覽後確認）`CAL.HOLIDAY.MANAGE`。
- 權限授與在「角色管理」手動進行；每個選單入口使用獨立權限碼。

## 前端
`FrontendFrameWork.RCL.Feature.Calendar`：`/sgs.modules.cal/calendar`（月檢視）、`/sgs.modules.cal/holidays`。BlazorWasmApp 已接線（csproj、`Program.cs`、`App.razor.cs`）。

## 注意
- 漏維護當年度假日會讓 PMS 期限誤判；修改假日不會回頭重算已分流問題單。
- 假日以年度快取（異動即失效，TTL 10 分鐘）。CAL 未安裝時 PMS 退回純週一至週五。
- 規劃：M2 部門與專案日曆、PMS 里程碑單向同步；M3 Email（含 .ics）與每日摘要。

## 測試
```
dotnet test Tests/SGS.Modules.CAL.Tests -c Release
```

## v1.1：政府辦公日曆同步與活動提醒
- 「假日與補班」頁的「同步政府辦公日曆」：預覽差異後寫入（只動「政府同步」來源的資料，人工維護且內容不同者預設保留）；`cal-holiday-sync` 每月 1 日 03:00 自動同步當年與明年。來源 `Calendar:GovSync:UrlTemplate`（https；{year}、{rocYear}），支援官方 CSV 與 TaiwanCalendar JSON；伺服器需能連外網。
- 活動可設與會者與最多 5 組提醒；`cal-event-reminders` 每 5 分鐘派送 Email（建立者＋與會者，需 SMTP 與使用者 Email）。
- 設定：`Calendar:Enabled=true` 才註冊兩個排程；排程帳號須授與 scope `cal.holiday.sync`、`cal.reminders.dispatch`（系統帳號管理）。
