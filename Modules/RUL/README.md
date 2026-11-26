# SGS.Modules.RUL — 動態規則引擎外掛模組

依《動態規則引擎實作指引》實作的通用規則引擎：資料權限（Row/Column ABAC）、動態表單、風險控管、計價折扣共用同一套「條件運算式 + 衝突合併」機制。**M1 只含引擎核心與管理後台，尚未接任何業務模組。**

## 結構
- Core 契約：`Shared/SGSFramework.Core/Rules/RuleEngineContracts.cs`（`IRuleEngine`、`IRuleSnapshotProvider`、`IRuleTargetCatalog`、`IRuleTester` 與 Null 預設）。其他模組只依賴 Core。
- 引擎函式庫：`Infrastructure/SGSFramework.RuleEngine`（Host 載入，持有 `System.Linq.Dynamic.Core`）：白名單檢查、編譯快取、合併策略、Row 轉 EF 述詞。`AddDynamicRuleEngine()` 於 Host `ServiceCollectionExtensions` 註冊。
- 本模組（`Module.RUL`／`.Application`／`.Infrastructure`，schema `rul`）：規則、變更申請、版本、測試案例；以 `RuleSnapshotProvider` 覆寫 Core 規則來源（快取 60 秒，核准即失效）。

## 運算式寫法
- Column／表單／風控／計價：`user`（`RuleUserContext`）與 `target`，例：`user.Level >= 3 && target.Amount > 50000`。
- Row：`row`（實體）與 `user`，例：`row.Department.Path.StartsWith(user.DepartmentPath)`。
- 白名單：僅 user 成員、已登錄目標實體成員，與 string／集合的少數方法（StartsWith、Contains、Any…）；禁反射、靜態類別、委派；長度上限 500。
- 目標實體須登錄：`services.AddRuleTarget<T>()`（M1 僅內建 `SampleRuleTarget` 供試跑）。

## 合併策略
權限 ForceDeny > Allow > Mask > Deny（無命中＝Deny）；風控 Block > ManualReview > Pass；表單顯示需全同意、必填任一成立；計價 BestPrice（最小倍率）或 Stack（連乘）。

## API 與權限
`POST api/v1/rul/rules/search`（`RUL.RULE.READ`，選單「動態規則」）、`validate`／`dry-run`／測試案例（`RUL.RULE.MANAGE`）；`POST api/v1/rul/changes/search`（`RUL.CHANGE.READ`，選單「規則審批」）、`submit`、`rollback`、`cancel`；`approve`／`reject` 需 `RUL.CHANGE.APPROVE`，且**申請者不可審核自己的申請**。權限於「角色管理」手動授與。DBA 腳本：`docs/sql/20261126_grant_rul_schema.sql`。

## 前端
`FrontendFrameWork.RCL.Feature.Rules`：`/sgs.modules.rul/rules`、`/sgs.modules.rul/changes`。

## 測試
```
dotnet test Tests/SGSFramework.RuleEngine.Tests -c Release
dotnet test Tests/SGS.Modules.RUL.Tests -c Release
```

## 尚未完成（M2/M3）
ORG 的「使用者→主管」關係與主管維護成員、`[SensitiveField]` 輸出遮罩與 `SaveChangesInterceptor` 寫入防線、通用 SecureField 表單元件、PMS 風險與 QTN 毛利燈號改由規則驅動、計價複雜公式。多節點規則失效目前僅靠 60 秒 TTL。**M1 未做實機驗證**（遷移未套用至真實資料庫、後台頁面未於瀏覽器操作）。

## M2-a：全區實驗室管理員（MO）僅維護自己的成員（使用者清單／詳情）
- 預設種子規則「全區實驗室管理員僅能維護自己的成員」（Row、目標 `ApplicationUser`、條件 `user.MemberUserIds.Contains(row.Id)`），啟動時於 RUL 遷移後建立（只在不存在時；之後修改須審批）。
- Identity 的 `UserMemberScopeService` 在使用者清單與詳情套用：系統管理員或沒有適用 Row 規則的角色不受限；MO 只看到成員，非成員詳情回 404。停用該規則即恢復全部可見。
- 「成員」＝與自己共同歸屬任一實驗室的使用者（`IOrganizationIntegrationService.GetMemberUserIdsAsync`，來源為 ORG 的可存取實驗室檢視），含自己。未建立 Employee／主管關係表；之後改為明確主管關係時只需換成員提供者。
- 尚未涵蓋：指派角色、直接權限、狀態變更、重設密碼、刪除等寫入端點（仍待 M2-b）。

## M2-b：成員範圍擴及權限總覽與寫入端點
- Core 新增 `IUserMemberScope` 與 `[RequiresMemberScope]`（以路由參數 `userId`／`id` 檢查目標是否為可維護成員，否則 404）。已標於：使用者管理的編輯、重設密碼、刪除、狀態、角色讀寫；權限管理的使用者權限總覽、直接權限、實驗室專屬權限；ORG 使用者實驗室歸屬的讀、設主要、移除。
- MO 預設種子新增 `SYSTEM.PERMISSION.READ`、`SYSTEM.PERMISSION.ASSIGN_USER`（appsettings 的 SystemRolePermissionSeed；啟動時對既有角色只增不減）。因 IncludeKeys 與 Categories 為 AND，若這兩個權限的類別不在 Operational／Administrative／Basic，需在角色管理手動授與。
- 注意：MO 指派角色／直接權限時是否能授與超過自己的權限，仍依既有治理機制，M2 未新增上限檢查。
