# SGS.Modules.FMS（檔案管理外掛模組）

企業級**無狀態安全儲存**子系統：多驅動儲存工廠（NAS / MinIO(S3) / Azure Blob）、Magic Number 驗證、圖片二次編碼、
AES-256-GCM 信封加密、傳輸簽章、動態浮水印檢視、記憶體縮圖快取、非同步存取稽核（Ledger）、虛擬刪除與排程清除。

設計依據：《企業級無狀態安全儲存子系統架構設計規格書》——傳輸帶簽章、儲存必加密、存取留日誌、瀏覽強水印。

## 外掛模組結構

| 專案 | 內容 |
| --- | --- |
| `Module.FMS`（SGS.Modules.FMS） | `FilesController`、`ModuleFmsInitializer`（IModuleInitializer） |
| `Module.FMS.Application` | `FileManagementService`、`FileMaintenanceService`、簽章檢查、Magick.NET 影像處理、記憶體縮圖快取、`Security/`（加解密串流、金鑰包裝）、`Watermark/`、`Auditing/` |
| `Module.FMS.Infrastructure` | `FMSDbContext`（schema `fms`）、`FmsMigrationService`、組態、儲存驅動工廠與 Provider、Migrations |

- 跨模組契約 `IFileManagementService` 與 DTO 位於 Core（`SGSFramework.Core.FileManagement`），由 Default ALC 共享。
- Core 預設註冊 `NullFileManagementService`（回應 503「模組未安裝」）；本模組載入時以 `AddScoped` 覆蓋。
- plugins 目錄只部署 `SGS.Modules.FMS*.dll`。第三方套件（Magick.NET、AWSSDK.S3、Azure.Storage.Blobs、PDFtoImage / PDFium、SkiaSharp，含 `runtimes/*/native`）必須由 Host 主程式目錄提供（PhysLIMS.API 參考本模組時會自動輸出），放進 plugins 會因簽章驗證被隔離。
- 啟動時由 `ModuleFmsInitializer` 以 MigrationConnection 套用 `fms` schema 遷移並授予執行期帳號 DML 權限。

## 架構

```
前端（Blazor / Vue，JWT）     Hangfire（SCH，M2M 系統帳號）
          │ HTTPS 二進位串流             │ OAuth2 Client Credentials
          ▼                             ▼
   FilesController (api/v1/files) ◄─────┘        業務模組（RPT 等，Internal scope）
          │  MultipartReader 串流（不經表單緩衝、不寫 Temp）       │
          ▼                                                       ▼
                         IFileManagementService
   預讀檔頭 → Magic Number → [圖片 / OOXML：記憶體緩衝（上限 + 併發限制）→ 重新編碼]
   → MeteredReadStream（SHA-256 / 長度 / 上限）→ EncryptingReadStream（AES-256-GCM 分塊，DEK 以 Data Protection 包裝）
   → 儲存驅動 SDK（無本機落地）→ fms.FileMetadatas → IFileAccessAuditor（佇列 → 背景批次寫入 fms.FileAccessLogs）
                                    │
                    IStorageFactory（依 FileManagement:Targets:{Target}:Provider）
           ┌────────────────────────┼────────────────────────┐
   FileSystem（地端 NAS / UNC）   S3（MinIO / AWS S3）      AzureBlob（Azure / Azurite）
```

| 元件 | 說明 |
| --- | --- |
| `FileMetadata`（`fms.FileMetadatas`） | 業務屬性與實體儲存屬性分離；`EncryptionVersion` / `WrappedKey` / `StoredSize` 記錄加密資訊；`IsDeleted` 虛擬刪除、`PurgedAtUtc` 實體清除時間 |
| `FileAccessLog`（`fms.FileAccessLogs`） | Ledger APPEND_ONLY：Upload / Download / View / Thumbnail / Delete / Purge / Encrypt / Denied / IntegrityFailure；`IsSystemAccount` + `ClientId` 區分人員與 M2M |
| `StorageFactory` | 依組態建立驅動（第一次使用時才建立，未啟用時不影響注入端建構）；`S3StorageProvider` 多段上傳 + Range 讀取 + 預簽名網址；`AzureBlobStorageProvider` 分塊上傳 + Range 讀取 + SAS |
| `FileCipherFormat` / `EncryptingReadStream` / `DecryptingReadStream` | SGSE v1：標頭 + 64KB 區塊（密文 + GCM Tag），nonce = 前綴 ‖ 序號，AAD 含標頭、序號與最後區塊旗標（防竄改、截斷、重排、拼接）；解密可定位時支援 HTTP Range |
| `FileKeyProtector` | 每檔隨機 256 位元 DEK，以 ASP.NET Data Protection（`SGS.Modules.FMS.FileKey.v1`）包裝存入中繼資料，明文金鑰用後清零 |
| `FileSignatureInspector` | 自製輕量簽章檢查：圖片 / PDF 檔頭、OOXML 結構（拒絕 VBA 巨集）、OLE2、UTF-8 文字；可執行檔一律拒絕 |
| `MagickImageProcessor` | 依檔頭明確指定解碼器（僅 JPEG / PNG / GIF / WebP / BMP / TIFF）；解碼前 Ping 檢查尺寸（防解壓縮炸彈），同格式重新編碼並 Strip 所有中繼資料 |
| `ImageCacheService` | 縮圖記憶體快取（`MemoryCache` + SizeLimit，滑動到期），分段鎖防 Stampede；不寫本機磁碟 |
| `WatermarkRenderer` | PDF：PDFtoImage（PDFium）逐頁渲染 → SkiaSharp 斜向平鋪浮水印繪入像素 → SKDocument 重組（維持原頁面尺寸）；圖片：Magick.NET 解碼後繪製 |
| `FileAccessAuditor` / `FileAccessLogWriter` | 請求當下擷取身分 / IP / 追蹤碼 → 有界佇列（滿時等待不丟棄）→ 背景批次寫入（失敗重試，最終寫入系統日誌）→ 關機排空 |
| `FileMaintenanceService` | 舊明文檔案轉加密：驗證原雜湊 → 加密寫入新鍵 → 更新中繼資料 → 刪除舊物件 |
| `FilePurgeJob`（位於排程外掛 SGS.Modules.SCH） | Hangfire 每日 04:00 以系統帳號呼叫 `POST api/v1/files/purge`；稽核紀錄自動標示為系統帳號 |

## API

| 方法 | 路徑 | 權限 |
| --- | --- | --- |
| POST | `api/v1/files?category=&referenceType=&referenceId=`（multipart：file；可附 `X-Content-SHA256`） | `FMS.FILE.UPLOAD` |
| POST | `api/v1/files/search` | `FMS.FILE.READ` |
| GET | `api/v1/files/{id}` | `FMS.FILE.READ` |
| GET | `api/v1/files/{id}/content?inline=`（`ViewOnly` 分類回 403） | `FMS.FILE.READ` |
| GET | `api/v1/files/{id}/view`（PDF / 圖片動態浮水印，`no-store`） | `FMS.FILE.READ` |
| GET | `api/v1/files/images/{id}?w=&h=&m=max\|crop\|pad` | `FMS.FILE.READ` |
| DELETE | `api/v1/files/{id}` | `FMS.FILE.DELETE` |
| POST | `api/v1/files/access-logs/search` | `FMS.FILE.AUDIT` |
| POST | `api/v1/files/purge` | `FMS.FILE.PURGE` 或系統帳號 scope `files.purge` |
| POST | `api/v1/files/maintenance/encrypt-legacy?maxFiles=` | `FMS.FILE.PURGE` 或系統帳號 scope `files.maintenance` |

- 通用端點只做功能權限。需要「依資料列授權」的業務（例如報告）應在業務模組先完成授權，再直接注入 `IFileManagementService`；`AllowGenericApi = false` 的分類在通用端點視同不存在。
- **傳輸簽章**：上傳可帶 `X-Content-SHA256`（原始內容 SHA-256，十六進位或 Base64），不符即拒絕並移除已寫入物件；下載回應 `Repr-Digest`、檢視回應 `Content-Digest`（RFC 9530）供用戶端驗證。
- 下載：地端機密為 `Cache-Control: private`，公開資源為 `public, immutable`；一律 `nosniff` 與 CSP `sandbox`，支援 ETag 與 Range（儲存串流可定位時）。
- 檢視：浮水印內容「使用者（系統帳號為 client_id）| UTC 時間 | IP」，因人因時而異，一律 `no-store`。

## 前端（Blazor RCL：`FrontendFrameWork.RCL.Feature.FileManagement`）

已實作於前端專案（`AddFileManagementFeature()`，頁面由 BlazorWasmApp 的 `AdditionalAssemblies` 載入）：

| 頁面 / 元件 | 路由 | 說明 |
| --- | --- | --- |
| 檔案清單 `FileManagement` | `/sgs.modules.fms/files` | 分頁查詢（POST search）、分類 / 上傳人員 / 含已刪除篩選；檢視、下載、刪除；顯示加密 / 儲存目標標示；`ViewOnly` 分類不顯示下載 |
| 上傳對話框 `FileUploadDialog` | — | 分類、允許格式與大小上限由 `GET files/categories` 動態取得；前端先行檢查，送出時附 `X-Content-SHA256` |
| 安全檢視 `FileViewerDialog` | — | 以帶 JWT 的 HTTP 取得 `files/{id}/view`，純記憶體 Blob 網址顯示 PDF / 圖片，關閉即 `revokeObjectURL`；停用右鍵 / 拖曳 / 選取、列印時隱藏 |
| 存取紀錄 `FileAccessLogs` | `/sgs.modules.fms/accesslog` | 動作 / 帳號類型（人員 / 系統帳號）/ 使用者 / 檔案 / 時間區間 / 僅失敗篩選；可由檔案識別碼追查單一檔案 |

- PDF 以 `<object>` 顯示（`#toolbar=0&navpanes=0`），**刻意不設 `pointer-events:none`**，否則多頁文件無法捲動；前端防護僅是使用者體驗層，實質防護仍是伺服器端點陣化浮水印與存取稽核。
- 選單由後端 `[Function(IsMenu = true, Path = ...)]` 產生；已被管理者自訂（`IsCustomized`）的功能不會被程式碼覆蓋。

## 前端串接（Vue 3 或其他前端）

- 上傳：`multipart/form-data`，分類等欄位放查詢字串；建議以 Web Crypto 計算 SHA-256 放入 `X-Content-SHA256`。
- 檢視：以帶 JWT 的 `fetch` / `HttpClient` 取得 `api/v1/files/{id}/view` 二進位，`URL.createObjectURL(blob)` 後交給 `<object>`，用畢 `revokeObjectURL`；不要將 Token 放入網址。
- 防右鍵 / 選取（`user-select: none`、`@contextmenu.prevent`）僅為使用者體驗層防護，真正的防外洩依賴伺服器端點陣化浮水印與存取稽核。

## 組態（`FileManagement`）

- `Enabled`：預設 `false`；關閉時所有操作回 503、不驗證儲存組態、不註冊清除排程。
- `Targets:{LocalPrivate|CloudPublic}`：`Provider` = `FileSystem`（需 `RootPath`，不可位於應用程式目錄 / wwwroot）/ `S3`（`ServiceUrl`、`Region`、`AccessKey`、`SecretKey`、`ForcePathStyle`）/ `AzureBlob`（`ConnectionString`）。
- `Categories`：儲存目標、副檔名、大小上限、`Encrypt`（預設 true）、`ViewOnly`、`AllowGenericApi`、快取秒數。
- `Upload`：需完整內容格式的記憶體緩衝上限（預設 50MB）與併發上限、加密分塊大小。
- `ThumbnailCache`：記憶體快取容量（預設 256MB）與滑動到期。
- `Watermark`：DPI（120）、頁數上限（50）、不透明度、JPEG 品質、字型檔。
- `Purge:RetentionDays`：虛擬刪除後保留天數（預設 30）。

正式環境以環境變數注入路徑與機密，例如：

```
FileManagement__Enabled=true
FileManagement__Targets__LocalPrivate__RootPath=\\nas01\physlims-private
FileManagement__Targets__LocalPrivate__Provider=S3
FileManagement__Targets__LocalPrivate__S3__ServiceUrl=https://minio.internal:9000
FileManagement__Targets__LocalPrivate__S3__AccessKey=...
FileManagement__Targets__LocalPrivate__S3__SecretKey=...
FileManagement__Targets__CloudPublic__Provider=AzureBlob
FileManagement__Targets__CloudPublic__AzureBlob__ConnectionString=...
```

本機驗證 MinIO / Azurite：`deploy/dev/fms-storage/docker-compose.yml`（帳密放 `.env`，已排除於版控）。

## 部署注意事項

1. **金鑰環即解密能力**：檔案資料金鑰由 Data Protection 金鑰環包裝。金鑰環遺失或毀損即無法解密任何檔案（與 `[Encrypted]` 個資欄位同一風險）；多節點 / 容器必須共用並持久保存金鑰環（`DataProtection:KeysPath` + 憑證保護），並納入備份。
2. **舊檔加密**：改版前的明文檔案仍可讀取；部署後以系統帳號（scope `files.maintenance`）或管理者重複呼叫 `maintenance/encrypt-legacy` 直到 `Remaining = 0`。
3. **原生程式庫**：Magick.NET、PDFium（`bblanchon.PDFium.*` 已指定較新版本以取得安全修補）、SkiaSharp 皆為原生解析器，請定期更新。SkiaSharp 須維持 2.88 系列（與 QuestPDF 共用）。只部署單一平台時可以 `-r win-x64` 發佈以縮小體積。
4. **NAS 執行權限**：儲存資料夾須於作業系統 / IIS / Nginx 層級關閉「執行」權限，僅授予應用程式集區帳號讀寫（修改）權限。
5. **系統帳號 Scope**：排程帳號需 `files.purge`；執行舊檔加密需 `files.maintenance`。
6. **記憶體**：縮圖快取、需完整內容格式的上傳緩衝與浮水印渲染皆於記憶體進行，依節點記憶體調整 `ThumbnailCache:MaxMemoryBytes`、`Upload:*`、`Watermark:*`。
