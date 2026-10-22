# 企業級 .NET 10 開發專案規範與 CLAUDE.md 協作指引

本檔案為 Claude Code 於此專案運作時的最高行為指導原則，所有生成的程式碼與系統架構設計均需嚴格遵守以下規範。

---

## 1. 技術棧與版本定義 (Tech Stack & Versions)

* **Framework**: .NET 10 (C# 14)
* **Architecture**: Clean Architecture (Domain, Application, Infrastructure, Presentation)
* **ORM**: Entity Framework Core 10 (EF Core 10)
* **UI Framework**: Blazor WebAssembly (WASM) 搭配 Razor Class Libraries (RCL)
* **Design System**: Microsoft Fluent UI Blazor Components
* **Testing Stack**: xUnit, NSubstitute, FluentAssertions
* **Time Management**: 嚴禁使用 `DateTime.Now`，統一採用 `TimeProvider` 進行抽象注入

---

## 2. 常用常用命令與快捷指令 (Development Commands)

使用標準指令進行專案建置、測試與程式碼風格校正：

```bash
# 建置整個 Solution
dotnet build --configuration Release

# 執行所有單元與整合測試
dotnet test --logger "console;verbosity=detailed"

# 執行程式碼自動格式化檢查與修正
dotnet format whitespace
dotnet format style

# Entity Framework Core 資料庫移轉 (於 Infrastructure 層執行)
dotnet ef migrations add <MigrationName> --project src/Infrastructure --startup-project src/WebAPI
dotnet ef database update --project src/Infrastructure --startup-project src/WebAPI
```

---

## 3. 程式碼撰寫與架構原則 (Coding & Architectural Principles)

### 3.1 職責分層 (Layer Responsibilities)
1. **Domain**: 僅包含 Domain Entities、Value Objects、Domain Events 與 Repository 介面。嚴禁依賴任何外部套件或基礎設施。
2. **Application**: 包含 Use Cases、DTOs、CQRS Handlers 與 Interface 定義。
3. **Infrastructure**: 實作 Application 層的介面，包含 EF Core DbContext、外部 API 介接與系統服務。
4. **Presentation**: API Controllers / Endpoints 與 Blazor UI 元件。

### 3.2 程式碼品質與安全規範
* **Null 檢查**: 預設開啟 `<Nullable>enable</Nullable>`，所有引用型別需明確宣告可空性並完成驗證。
* **非同步程式設計**: 涉及 I/O 操作必須完整透傳 `CancellationToken`，使用 `async/await` 語法糖。
* **錯誤處理與日誌**: 使用全域 Exception Handling 中間件；日誌記錄時必須落實敏感資訊（PII）遮蔽與安全防護。
* **輕量化架構**: 優先採用自訂輕量化模式（如自訂 Result 模式或 CQRS Dispatcher），避免引進非必要的第三方套件。

---

## 4. WHAT-WHY-HOW 契約格式 (Task Delivery Contract)

每當提報變更或生成代碼時，必須於說明文件中遵守以下格式結構：

* **WHAT**: 說明本次修改或新增的具體功能與範圍。
* **WHY**: 說明變更背景、業務需求或修正的根本原因。
* **HOW**: 說明具體採用的架構設計、模式（如 Strategy Pattern）與相關模組的影響。

---

## 5. 安全與存取控制原則 (Security & Access Control)

* **機密資訊保護**: 嚴禁將密碼、API Key 或連線字串寫死於程式碼中，敏感設定必須經由環境變數或受保護的 `appsettings.Development.json` / `claudesettings.json` 讀取。
* **權限控管**: 涉及檔案刪除、 destructive 命令或部署動作時，必須主動尋求人類確認 (Ask Mode)。

## 6. 語言與對話規範 (Language & Communication Policy)
* **預設語言**: 所有與使用者進行的對話、技術說明、註解與 `WHAT-WHY-HOW` 報告，**必須統一使用繁體中文（Traditional Chinese）**回覆。
* **術語對照**: 專業技術名詞（如 Clean Architecture、DTO、CQRS、Middleware、Blazor）可保留英文或業界標準術語，但說明與解釋的內文必須為流暢的繁體中文。