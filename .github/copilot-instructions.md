# .NET / C# 專案開發規範與全域指示

你目前正在協助一個 C# 專案（解決方案名稱：SGSFramework）。在為此專案產生、重構、檢查或修改程式碼時，必須嚴格遵守以下規範。

## 1. 技術堆疊與語法特徵
- **目標框架**：專案基於現代 .NET（請優先採用 .NET 8 / 9+ 的新特性）。
- **現代 C# 語法特徵**：
  - 類別宣告：在適當的情況下，優先使用**主建構函式 (Primary Constructors)**。
  - 命名空間：一律使用**檔案範圍命名空間 (File-scoped namespaces)**（例如 `namespace SGSFramework.Services;`），不加外層大括號。
  - 集合：初始化集合時優先使用**集合運算式 (Collection expressions)**（例如 `int[] numbers = [1, 2, 3];`）。
  - 型別推導：本地變數當型別明確時，優先使用 `var`。

## 2. 程式碼風格與格式化 (Coding Conventions)
- **大括號風格**：採用 **Allman 風格**（右括號與左括號各自獨立一行，並與目前縮排對齊）。
- **縮排**：一律使用 4 個空格，不使用 Tab 字元。
- **異步編程**：所有的 I/O 密集型操作（如資料庫存取、外部 API 請求、檔案讀寫）一律使用 `async` / `await`，且方法命名後綴必須帶有 `Async`（例如 `GetDataAsync`）。

## 3. 架構與設計原則
- **相依性注入 (Dependency Injection)**：優先使用建構函式注入（Constructor Injection）來傳遞依賴。
- **多層架構規範**：
  - 控制器 (Controllers/API) 僅負責請求分發與驗證，業務邏輯必須嚴格封裝於 Services 層。
  - 資料存取請遵循 Repository / Entity Framework Core 規範，禁止在 Controller 內直接撰寫 LINQ 查詢資料庫。
  - 跨層傳輸資料時，必須使用 DTO (Data Transfer Object)，嚴禁直接將實體模型 (Entity Model) 暴露給前端。

## 4. 錯誤處理與日誌紀錄
- **異常控制**：使用結構化的 `try-catch` 區塊捕捉預期內的異常。嚴禁空捕捉 (Empty catch) 或直接吞掉錯誤 (Swallow exception)。
- **日誌**：透過注入 `ILogger<T>` 進行日誌紀錄。在 catch 區塊中，必須使用 `_logger.LogError(ex, "錯誤訊息")` 完整記錄 Exception 堆疊。

## 5. Agent 協作行為規範（當處於 Agent 模式時）
- **自動化測試與糾錯**：當你修改完程式碼或完成重構後，必須自動在 VS Code 整合終端機中執行 `dotnet build` 或 `dotnet test`。
- **錯誤回退**：如果執行測試發現報錯，請利用編譯錯誤訊息自動進行至少 2 次自我修正，確認成功編譯再向使用者回報進度。
- **回應語言**：所有對話與程式碼註解說明，一律使用繁體中文（zh-TW）。
