# MTG 會議助理外掛模組（SGS.Modules.MTG）

逐字稿管理、場景 Prompt 模板、術語校正，以及以本機 Ollama 執行的 Map-Reduce 會議紀要。資料全程不出內網。
規劃來源：《企業級全本地端 AI 語音與會議助理》。目前完成 **M1**（文字流程）與 **M2**（音檔上傳與轉錄、桌面端同步 API）；桌面端程式、待辦通知與 PMS 整合見後續里程碑。

## 流程
逐字稿有三個來源，進來後都會自動套用術語表校正：貼上文字（M1）、上傳音檔轉錄（M2）、桌面端批次上傳片段（M2）。

建立會議 → 取得逐字稿 → 要求產生紀要（非同步，進入佇列）→ 背景工作者 Map-Reduce → 新增紀要版本 → 會議狀態 `Done`。

音檔：上傳（以 AES-256-GCM 加密存入 FMS）→ 轉錄佇列 → 背景工作者讀回音檔、呼叫轉錄服務 → 寫入逐字稿 → `TranscriptReady`；上傳時可勾選「轉錄完成後自動產生紀要」。

狀態：`Draft` → `TranscriptReady` ⇄ `Transcribing`／`Summarizing` → `Done` / `Failed`。`Transcribing` 與 `Summarizing` 期間會議不可修改、刪除、寫入逐字稿或再次上傳（回 409）。服務重啟時仍在這兩個狀態的會議會被標為 `Failed`（佇列在記憶體；逐字稿與音檔都不會遺失），音檔可直接「重新轉錄」。

## 專案
| 專案 | 內容 |
|---|---|
| `Module.MTG` | Controllers、`ModuleMtgInitializer` |
| `Module.MTG.Application` | 服務、規則（切塊 / 術語校正 / 逐字稿解析）、`MapReduceSummarizer`、`OllamaClient`、`MinutesWorker` |
| `Module.MTG.Infrastructure` | `MTGDbContext`（schema `mtg`）、實體、migration、`MtgMigrationService` |

測試：`Tests\SGS.Modules.MTG.Tests`（純單元測試）。

## 資料表（schema `mtg`）
`Meetings`、`TranscriptSegments`（`AudioId` 記錄來源音檔、`ClientSegmentId` 供桌面端冪等）、`MeetingAudios`（FMS 檔案關聯與轉錄狀態）、`MeetingMinutes`（每次產生新增一版）、`PromptTemplates`（內建四場景，啟動時補齊、不覆蓋已調整者）、`GlossaryEntries`、`ActionItems`（待辦；`OwnerEmail` 為個資，`[Encrypted]` 落地加密；`PmsTaskId` 記錄回寫的 PMS 任務）、`OutboxMessages`。`MeetingAudios` 的狀態另有 `Expired`（錄音已依保存期限刪除）。

## API 與權限
| 端點 | 權限 |
|---|---|
| `POST mtg/meetings/search`、`GET mtg/meetings/{id}`、`GET mtg/templates` | `MTG.MEETING.READ` |
| `POST/PUT mtg/meetings`、`POST mtg/meetings/{id}/transcript` | `MTG.MEETING.CREATE` / `UPDATE` |
| `DELETE mtg/meetings/{id}` | `MTG.MEETING.DELETE` |
| `GET mtg/meetings/{id}/transcript` | `MTG.TRANSCRIPT.READ`（逐字稿敏感，獨立授權） |
| `POST mtg/meetings/{id}/minutes`（回 202） | `MTG.MINUTES.GENERATE` |
| `GET mtg/meetings/{id}/audio` | `MTG.MEETING.READ` |
| `POST mtg/meetings/{id}/audio`（multipart，回 202）、`POST .../audio/{audioId}/retranscribe` | `MTG.AUDIO.UPLOAD` |
| `GET .../audio/{audioId}/content`（下載原檔） | `MTG.AUDIO.DOWNLOAD` |
| `DELETE .../audio/{audioId}?removeTranscript=` | `MTG.AUDIO.DELETE` |
| `POST mtg/meetings/{id}/segments`（桌面端批次，冪等，≤500 筆）、`POST .../finalize`、`GET mtg/glossary/terms` | `MTG.CLIENT.UPLOAD` |
| `GET mtg/meetings/{id}/minutes`、`.../minutes/latest?version=` | `MTG.MINUTES.READ` |
| `POST mtg/action-items/search`（跨會議，可篩逾期）、`GET mtg/meetings/{id}/action-items` | `MTG.ACTION.READ` |
| `POST/PUT/DELETE mtg/meetings/{id}/action-items`、`POST .../action-items/extract` | `MTG.ACTION.MANAGE` |
| `POST mtg/meetings/{id}/action-items/notify`（寄信） | `MTG.ACTION.NOTIFY`（Critical） |
| `POST mtg/meetings/{id}/action-items/sync-pms`（回寫 PMS WBS 任務） | `MTG.ACTION.SYNC_PMS` |
| `POST mtg/audio/purge-expired`（排程以系統帳號 scope `mtg.audio.purge` 呼叫，亦可手動） | `MTG.AUDIO.PURGE` |
| `GET mtg/meetings/project-options?keyword=`（可關聯的 PMS 專案） | `MTG.MEETING.READ` |
| `POST/PUT mtg/templates/{scenarioCode}` | `MTG.TEMPLATE.MANAGE` |
| `POST mtg/glossary/search` | `MTG.GLOSSARY.READ` |
| `POST/PUT/DELETE mtg/glossary` | `MTG.GLOSSARY.MANAGE` |

## 設定（區段 `Meeting`，見 `Host\PhysLIMS.API\appsettings.json`）
預設 `Enabled=false`：關閉時仍可維護會議與逐字稿，僅無法產生紀要。啟用後 Ollama 位址只允許本機或內網（`InternalUrlRules`，與 MAN 的規則相同、需同步維護；轉錄服務位址也套用同一規則）。
`NumCtx=6144`、`ChunkChars=4500`、`OverlapChars=300` 為規劃預設；中文每字約 1～1.5 token，若模型回應被截斷請調低 `ChunkChars` 或調高 `NumCtx`。
環境變數覆寫：`Meeting__Enabled=true`。

### 語音轉錄（`Meeting:Whisper`）
預設 `Enabled=false`：關閉時仍可貼上逐字稿，但不能上傳音檔。伺服器不內嵌推論程式庫（外掛的第三方原生套件會被簽章檢查隔離），而是呼叫本機／內網的 **OpenAI 相容轉錄服務**：`POST {BaseUrl}{Path}`（預設 `/v1/audio/transcriptions`），multipart 欄位 `file`、`model`、`language`、`response_format=verbose_json`、`temperature`、`prompt`（由術語表產生，引導中英夾雜與專有名詞辨識）。
whisper.cpp server（`--inference-path /v1/audio/transcriptions`）、faster-whisper-server、Speaches、LocalAI 皆可。位址只允許本機或內網。回應優先使用 `segments`（含起訖秒數），只回 `text` 時依標點切段。
音檔整檔讀入記憶體（不落地）後送出，上限 `MaxAudioBytes`（預設 100 MB，須 ≤ FMS 分類 `meeting-audio` 的上限）。環境變數：`Meeting__Whisper__Enabled=true`、`Meeting__Whisper__BaseUrl=http://主機:8080`。
**繁體中文輸出**：實測 Whisper 沒有提示詞時整段輸出簡體；任何繁體提示詞都能讓輸出改為繁體。所以中文（`Language` 為 zh 或自動偵測）轉錄一律帶一句「以下是繁體中文的會議記錄。」，再附加術語表的術語；術語表是空的也一樣。語言設為非中文時不帶此句。輸出的用字仍是模型的選擇（例如「周會」而非「週會」），可在術語表加誤聽詞（`周會` → `週會`）統一。辨識錯字（例如把「遷移」聽成「簽」）同樣用術語表的誤聽詞修正；術語本身進提示詞後也會提高辨識正確率。

**實測效能**（本機純 CPU、large-v3-turbo q5_0、8 執行緒）：17.8 秒的音檔約 25～37 秒完成，約為音檔長度的 1.4～2 倍。一小時的會議需要約 1.5～2 小時，所以 `TimeoutMinutes` 預設 180；音檔更長或想加快，請改用較小的模型（small q5_1 約 190 MB）或有 GPU／OpenVINO 的主機。

### 本機安裝 whisper-server（Windows）
1. 下載 whisper.cpp 發行檔 `whisper-bin-x64.zip`（github.com/ggml-org/whisper.cpp releases，CPU 版，內含 `whisper-server.exe`）與模型 `ggml-large-v3-turbo-q5_0.bin`（huggingface.co/ggerganov/whisper.cpp）。
2. 以 PowerShell 啟動：`whisper-server.exe -m <模型> --host 127.0.0.1 --port 8081 --inference-path /v1/audio/transcriptions -t 8 -l zh`。
3. **在 Git Bash 啟動要加 `MSYS_NO_PATHCONV=1`**，且模型路徑用 Windows 格式；否則 `--inference-path` 會被轉成 `C:/Program Files/Git/...` 導致 404。
4. 設定 `Meeting__Whisper__Enabled=true`、`Meeting__Whisper__BaseUrl=http://localhost:8081`。
5. wav 以外的格式（mp3、m4a…）要加 `--convert` 並在伺服器安裝 ffmpeg；否則請先轉成 16 kHz 單聲道 wav。

### FMS 音訊支援
FMS 的簽章檢查已擴充 wav、mp3、m4a、ogg、flac、webm（只驗證容器檔頭，不解碼）。須在 FMS 分類明確開放，`appsettings.json` 已加入分類 `meeting-audio`（加密、非通用 API、100 MB）。

## 待辦事項與 PMS 專案（M5）
- **抽取**：紀要產生完成後，`MinutesWorker` 自動抽取一次（失敗只記錄，不影響紀要）；也可手動重新抽取。先解析紀要中標題含「待辦」的表格（依表頭辨識欄位）；沒有表格（客戶訪談、架構評審、週會場景）才請模型輸出 JSON。「下週五」「11/30」「12月4日前」等期限會依**會議當天（台灣時間）**換算成日期，「第三周」「盡快」這類無法確定的只保留原文，不猜。
- **重新抽取的合併規則**：只取代「沒被編輯過、未通知、狀態仍是 Open 的抽取結果」；人工新增、編輯過、已通知或已處理的一律保留；內容相同（忽略標點與大小寫）的不重複加入。
- **通知**：有負責人信箱的待辦各寄一封給該負責人（同一人合併）；另可給最多 10 位額外收件人一封完整清單。收件人逐一驗證（拒絕換行、多地址、`名稱 <a@b>` 格式以防標頭注入）、內容一律 HTML 編碼、主旨去除換行；日誌與錯誤訊息只出現遮蔽後的信箱。只有寄成功的待辦會記錄通知時間。SMTP 沿用系統設定（`AlertSettings:Smtp`）；未設定回 503。
- **PMS 專案關聯**：MTG 只依賴 Core 契約 `IMeetingProjectLookup`（預設 `NullMeetingProjectLookup`，PMS 載入時覆蓋，與 `IQuoteContractIntegration` 同模式）。沿用 PMS 的資料列授權：只能關聯**自己是成員的進行中專案**（具 `PMS.PROJECT.ADMIN` 者可見全部）；不存在與看不到的專案回同一則錯誤，不洩漏專案是否存在；沿用原本已關聯的專案（未變更）不重驗；看不到的專案只回識別碼，不回名稱。
- 注意：Core 新增了型別，**修改後須重建 Host、PMS、MTG 並重啟 API**，否則 MTG 啟動時會因找不到 `IMeetingProjectLookup` 失敗。

## 錄音保存期限與 PMS 回寫（M6）
- **錄音保存期限**：`Meeting:AudioRetentionDays`（預設 0 = 不自動刪除）。大於 0 時，SCH 每日 04:30 執行 `mtg-audio-retention`，呼叫 `POST mtg/audio/purge-expired`：已轉錄或轉錄失敗、超過期限的錄音從 FMS 刪除（虛擬刪除，實體檔由 FMS 的保留期排程再清），狀態改為 `Expired`，**逐字稿與紀要保留**。先刪 FMS 再標記，FMS 失敗的下次再試；FMS 回報不存在也視為已清除；排隊中、轉錄中的不碰；單次最多 200 筆。已過期的錄音下載回 410、不能重新轉錄（409）。排程帳號須在系統帳號管理授與 scope `mtg.audio.purge`。
- **回寫 PMS**：`POST mtg/meetings/{id}/action-items/sync-pms` 把未完成、尚未回寫的待辦建立成會議所關聯專案的 WBS 任務，放在甘特圖頂層的「會議待辦」群組下（起日＝會議日、結束日＝待辦期限，沒有期限就 7 天）。MTG 只依賴 Core 契約 `IMeetingTaskWriteback`（預設 Null，PMS 覆蓋），實作經由 `GanttService.CreateTaskAsync`，所以沿用甘特圖的全部規則：只有專案 PM／Tech Lead 可建立、專案須進行中、非成員一律當作專案不存在。**冪等**：每個任務說明帶 `[MTG:待辦識別碼]` 標記，重送會找回既有任務而不重複建立，即使 MTG 沒記到也一樣。**單向、只建立**：回寫後兩邊各自修改不會同步；已回寫的待辦重新抽取時不會被刪除。
- **負責人不會自動指派**：PMS 專案成員只有使用者識別碼，伺服器端沒有可靠的「姓名 → 使用者」對應；紀要中的負責人姓名寫在任務說明，由 PM 在甘特圖指派。
- **不做**：伺服器端聲紋辨識（需要聲紋模型與原生執行階段，外掛不能放原生套件）。桌面端用聲道區分我方與與會者；伺服器端音檔轉錄的片段不標講者。

## 部署
1. DBA 執行 `docs\sql\20261121_grant_mtg_schema.sql`（或讓部署帳號自動授權）。
2. 建置後 `SGS.Modules.MTG*.dll` 由 `CopyModuleDllToHost` 複製到 Host 的 `plugins`；模組含背景工作者，更新須重啟 API。

## 後續里程碑
伺服器端聲紋辨識（需求不明確且需另外取得模型，暫不做）。
