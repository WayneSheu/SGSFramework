# SGS.Modules.FMS（檔案管理外掛模組）

生產級檔案管理外掛模組：多儲存體工廠分流、Magic Number 驗證、圖片二次編碼、動態縮圖 LRU 快取、虛擬刪除與排程清除。

## 外掛模組結構

| 專案 | 內容 |
| --- | --- |
| `Module.FMS`（SGS.Modules.FMS） | `FilesController`、`ModuleFmsInitializer`（IModuleInitializer） |
| `Module.FMS.Application` | `FileManagementService`、簽章檢查、Magick.NET 影像處理、縮圖快取與 LRU 清理 |
| `Module.FMS.Infrastructure` | `FMSDbContext`（schema `fms`）、`FmsMigrationService`、組態、儲存體工廠與 Provider、Migrations |

- 跨模組契約 `IFileManagementService` 與 DTO 位於 Core（`SGSFramework.Core.FileManagement`），由 Default ALC 共享。
- Core 預設註冊 `NullFileManagementService`（回應 503「模組未安裝」）；本模組載入時以 `AddScoped` 覆蓋。
- plugins 目錄只部署 `SGS.Modules.FMS*.dll`。Magick.NET（受管組件與 `runtimes/*/native` 原生程式庫）必須由 Host 主程式目錄提供（PhysLIMS.API 參考本模組時會自動輸出），放進 plugins 會因簽章驗證被隔離。
- 啟動時由 `ModuleFmsInitializer` 以 MigrationConnection 套用 `fms` schema 遷移。

## 架構

```
FilesController (api/v1/files) ─┐
業務模組（注入服務）─────────────┤
                                 ▼
                     IFileManagementService
   分類分流 → 大小 → Magic Number → Magick.NET 重新編碼 → SHA-256 → Guid 檔名 → 中繼資料 (core.FileMetadatas)
                                 │
                     IStorageFactory（依 StorageTarget 分派）
                    ┌────────────┴────────────┐
             LocalPrivate                CloudPublic
        FileSystemStorageProvider   FileSystemStorageProvider（未來可換 S3 / Azure Blob）
```

| 元件 | 說明 |
| --- | --- |
| `FileMetadata`（`fms.FileMetadatas`） | 業務屬性與實體儲存屬性分離；`IsDeleted` 虛擬刪除、`PurgedAtUtc` 實體清除時間 |
| `IStorageProvider` / `StorageFactory` | 依 `FileManagement:Targets` 建立；檔案系統實作含路徑遍歷防護與原子寫入 |
| `FileSignatureInspector` | 自製輕量簽章檢查：圖片 / PDF 檔頭、OOXML 結構（拒絕 VBA 巨集）、OLE2、UTF-8 文字；可執行檔一律拒絕 |
| `MagickImageProcessor` | 依檔頭明確指定解碼器（僅 JPEG / PNG / GIF / WebP / BMP / TIFF，不用 ImageMagick 自動偵測）；解碼前 Ping 檢查尺寸（防解壓縮炸彈），同格式重新編碼（動畫保留影格）並 Strip 所有中繼資料 |
| `ImageCacheService` | 縮圖 Cache-Aside（本機 SSD）、分段鎖防 Stampede、命中時更新 LastAccessTime |
| `ImageCacheLruCleanupService` | 各節點常駐，超過高水位依最後存取時間刪到低水位 |
| `FilePurgeJob`（位於排程外掛 SGS.Modules.SCH） | Hangfire 每日 04:00 以系統帳號呼叫 `POST api/v1/files/purge`；Hangfire 僅序列化 Core 的 `IScheduledJobDispatcher`，不解析外掛 ALC 型別 |

## API

| 方法 | 路徑 | 權限 |
| --- | --- | --- |
| POST | `api/v1/files`（multipart：file、category、referenceType、referenceId） | `FMS.FILE.UPLOAD` |
| POST | `api/v1/files/search` | `FMS.FILE.READ` |
| GET | `api/v1/files/{id}` | `FMS.FILE.READ` |
| GET | `api/v1/files/{id}/content?inline=` | `FMS.FILE.READ` |
| GET | `api/v1/files/images/{id}?w=&h=&m=max\|crop\|pad` | `FMS.FILE.READ` |
| DELETE | `api/v1/files/{id}` | `FMS.FILE.DELETE` |
| POST | `api/v1/files/purge` | `FMS.FILE.PURGE` 或系統帳號 scope `files.purge` |

通用端點只做功能權限。需要「依資料列授權」的業務（例如報告附件）應在業務模組先完成授權，再直接注入 `IFileManagementService`。

下載回應：地端機密為 `Cache-Control: private`，公開資源為 `public, immutable`；一律加 `X-Content-Type-Options: nosniff` 與 CSP `sandbox`，支援 Range 與 ETag（SHA-256）。

## 組態（`FileManagement`）

- `Enabled`：預設 `false`；關閉時所有操作回 503、不驗證儲存組態、不註冊清除排程。
- `Targets:{LocalPrivate|CloudPublic}:RootPath`：必須為絕對路徑或 UNC，且不可位於應用程式目錄或 wwwroot（啟動時驗證）。
- `Categories`：分類決定儲存目標、允許副檔名、大小上限、快取秒數；用戶端無法自行指定儲存目標。
- `Image:AllowedSizes`：縮圖尺寸白名單（`WxH`，0 表示依比例）。
- `ImageCache`：本機快取目錄與高 / 低水位（預設 40GB / 30GB）。
- `Purge:RetentionDays`：虛擬刪除後保留天數（預設 30）。

正式環境以環境變數注入路徑，例如：

```
FileManagement__Enabled=true
FileManagement__Targets__LocalPrivate__RootPath=\\nas01\physlims-private
FileManagement__Targets__CloudPublic__RootPath=\\nas01\physlims-public
FileManagement__ImageCache__RootPath=D:\PhysLIMS\ImageCache
```

## 部署注意事項

1. **影像函式庫**：使用 `Magick.NET-Q8-AnyCPU`（Apache-2.0，不需授權金鑰）。2026-11-04 取代 `SixLabors.ImageSharp`，原因是 3.2 起 Release 建置須商業授權金鑰，而 3.1.x 有已知高風險弱點。
   發佈輸出包含各平台原生程式庫（`runtimes/{rid}/native/Magick.Native-*`）；只部署單一平台時可以 `-r win-x64` 等 RuntimeIdentifier 發佈以縮小體積。ImageMagick 為原生解析器，請定期更新套件以取得安全修補。
2. **NAS 執行權限**：儲存資料夾須於作業系統 / IIS / Nginx 層級關閉「執行」權限，僅授予應用程式集區帳號讀寫（修改）權限。
3. **系統帳號 Scope**：啟用清除排程前，請於「系統帳號管理」為排程帳號加上 `files.purge`。
4. **快取目錄**：屬可重建的衍生資料，不需備份；多節點時每台各自一份。
