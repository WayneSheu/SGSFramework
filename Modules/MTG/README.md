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
`Meetings`、`TranscriptSegments`（`AudioId` 記錄來源音檔、`ClientSegmentId` 供桌面端冪等）、`MeetingAudios`（FMS 檔案關聯與轉錄狀態）、`MeetingMinutes`（每次產生新增一版）、`PromptTemplates`（內建四場景，啟動時補齊、不覆蓋已調整者）、`GlossaryEntries`、`OutboxMessages`。

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

## 部署
1. DBA 執行 `docs\sql\20261121_grant_mtg_schema.sql`（或讓部署帳號自動授權）。
2. 建置後 `SGS.Modules.MTG*.dll` 由 `CopyModuleDllToHost` 複製到 Host 的 `plugins`；模組含背景工作者，更新須重啟 API。

## 後續里程碑
M3～M4 桌面端 LocalVoiceAssistant；M5 待辦抽取、IMailSender 與 PMS 專案關聯；M6 伺服器端聲紋、PMS 回寫與排程清理過期音檔（音檔保存天數目前沿用 FMS 的刪除保留期，MTG 尚無自動過期）。
