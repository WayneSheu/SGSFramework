using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.MTG.Infrastructure.Entities;

/// <summary>會議處理狀態（API 一律以字串傳遞，與 MAN 模組一致）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MeetingStatus
{
    /// <summary>草稿：尚無逐字稿。</summary>
    Draft = 0,
    /// <summary>逐字稿已就緒，可產生紀要。</summary>
    TranscriptReady = 1,
    /// <summary>紀要產生中（佇列或模型處理）。</summary>
    Summarizing = 2,
    /// <summary>音檔轉錄中（佇列或轉錄服務處理）。</summary>
    Transcribing = 4,
    /// <summary>已產生紀要。</summary>
    Done = 3,
    /// <summary>處理失敗，可重新產生。</summary>
    Failed = 9
}

[DisplayName("會議")]
[Comment("會議主檔：標題、場景、處理狀態與可選的 PMS 專案關聯")]
public class Meeting : IAuditable
{
    [DisplayName("識別主鍵")]
    [Comment("識別主鍵")]
    public Guid Id { get; set; }

    [DisplayName("會議標題")]
    [Comment("會議標題")]
    public string Title { get; set; } = string.Empty;

    [DisplayName("場景代碼")]
    [Comment("會議場景代碼（對應 PromptTemplate.ScenarioCode，例如 general、client-discovery）")]
    public string ScenarioCode { get; set; } = "general";

    [DisplayName("會議時間")]
    [Comment("會議開始時間（UTC）")]
    public DateTimeOffset MeetingTimeUtc { get; set; }

    [DisplayName("PMS 專案")]
    [Comment("關聯的 PMS 專案識別碼（可空；M5 起使用）")]
    public Guid? ProjectId { get; set; }

    [DisplayName("處理狀態")]
    [Comment("處理狀態：Draft、TranscriptReady、Summarizing、Done、Failed")]
    public MeetingStatus Status { get; set; } = MeetingStatus.Draft;

    [DisplayName("狀態訊息")]
    [Comment("失敗原因等狀態說明（不含逐字稿或模型內容）")]
    public string? StatusMessage { get; set; }

    [DisplayName("主辦人")]
    [Comment("建立會議的使用者")]
    public string Organizer { get; set; } = string.Empty;

    [DisplayName("建立人員")]
    [Comment("建立人員")]
    public string CreatedBy { get; set; } = string.Empty;

    [DisplayName("建立時間")]
    [Comment("建立時間")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [DisplayName("最後異動人員")]
    [Comment("最後異動人員")]
    public string? UpdatedBy { get; set; }

    [DisplayName("最後異動時間")]
    [Comment("最後異動時間")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public List<TranscriptSegment> Segments { get; set; } = [];
    public List<MeetingMinutes> MinutesVersions { get; set; } = [];
    public List<MeetingAudio> Audios { get; set; } = [];
    public List<ActionItem> ActionItems { get; set; } = [];
}

[DisplayName("逐字稿片段")]
[Comment("逐字稿片段：依序號排列，保留原文與術語校正後文字")]
public class TranscriptSegment
{
    [DisplayName("識別主鍵")]
    [Comment("識別主鍵")]
    public Guid Id { get; set; }

    [DisplayName("會議")]
    [Comment("所屬會議")]
    public Guid MeetingId { get; set; }

    [DisplayName("序號")]
    [Comment("片段序號（從 1 起，會議內唯一）")]
    public int Seq { get; set; }

    [DisplayName("講者")]
    [Comment("講者標籤（我方、與會者 A…；可空）")]
    public string? Speaker { get; set; }

    [DisplayName("開始秒數")]
    [Comment("相對會議開始的起始秒數（可空）")]
    public double? StartSeconds { get; set; }

    [DisplayName("結束秒數")]
    [Comment("相對會議開始的結束秒數（可空）")]
    public double? EndSeconds { get; set; }

    [DisplayName("原文")]
    [Comment("原始逐字稿文字")]
    public string Text { get; set; } = string.Empty;

    [DisplayName("校正後文字")]
    [Comment("術語表校正後的文字")]
    public string? CorrectedText { get; set; }

    [DisplayName("用戶端片段識別")]
    [Comment("桌面端產生的冪等識別碼（用於去重；可空）")]
    public string? ClientSegmentId { get; set; }

    [DisplayName("來源音檔")]
    [Comment("由哪個音檔轉錄而來（可空：貼上或桌面端上傳的片段沒有來源音檔）；重新轉錄時只取代同一音檔的片段")]
    public Guid? AudioId { get; set; }

    public Meeting? Meeting { get; set; }
}

/// <summary>音檔轉錄狀態。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AudioStatus
{
    /// <summary>已上傳，排隊等待轉錄。</summary>
    Queued = 0,
    /// <summary>轉錄中。</summary>
    Transcribing = 1,
    /// <summary>已轉錄。</summary>
    Transcribed = 2,
    /// <summary>錄音已依保存期限刪除（逐字稿保留）；無法下載或重新轉錄。</summary>
    Expired = 3,
    /// <summary>轉錄失敗，可重新轉錄。</summary>
    Failed = 9
}

[DisplayName("會議音檔")]
[Comment("會議錄音：實際檔案以加密形式存放於檔案管理（FMS），此表只存關聯與轉錄狀態")]
public class MeetingAudio : IAuditable
{
    [DisplayName("識別主鍵")]
    [Comment("識別主鍵")]
    public Guid Id { get; set; }

    [DisplayName("會議")]
    [Comment("所屬會議")]
    public Guid MeetingId { get; set; }

    [DisplayName("FMS 檔案")]
    [Comment("檔案管理（FMS）的檔案識別碼")]
    public Guid FileId { get; set; }

    [DisplayName("原始檔名")]
    [Comment("上傳時的原始檔名")]
    public string FileName { get; set; } = string.Empty;

    [DisplayName("內容類型")]
    [Comment("FMS 依檔頭簽章判定的 MIME 類型")]
    public string ContentType { get; set; } = string.Empty;

    [DisplayName("檔案大小")]
    [Comment("位元組")]
    public long FileSize { get; set; }

    [DisplayName("SHA-256")]
    [Comment("原始內容雜湊（十六進位）")]
    public string Sha256 { get; set; } = string.Empty;

    [DisplayName("轉錄狀態")]
    [Comment("Queued、Transcribing、Transcribed、Failed")]
    public AudioStatus Status { get; set; } = AudioStatus.Queued;

    [DisplayName("狀態訊息")]
    [Comment("失敗原因（不含逐字稿內容）")]
    public string? StatusMessage { get; set; }

    [DisplayName("音檔長度")]
    [Comment("秒；轉錄服務回報時才有")]
    public double? DurationSeconds { get; set; }

    [DisplayName("辨識語言")]
    [Comment("轉錄服務回報的語言代碼（可空）")]
    public string? Language { get; set; }

    [DisplayName("片段數")]
    [Comment("此音檔轉錄出的逐字稿片段數")]
    public int SegmentCount { get; set; }

    [DisplayName("轉錄完成時間")]
    [Comment("UTC")]
    public DateTimeOffset? TranscribedAtUtc { get; set; }

    [DisplayName("過期刪除時間")]
    [Comment("錄音依保存期限（Meeting:AudioRetentionDays）從 FMS 刪除的時間（UTC）；未過期為空")]
    public DateTimeOffset? ExpiredAtUtc { get; set; }

    [DisplayName("轉錄後自動產生紀要")]
    [Comment("上傳時是否勾選；轉錄成功後由工作者接續要求產生紀要")]
    public bool AutoSummarize { get; set; }

    [DisplayName("自動紀要場景")]
    [Comment("自動產生紀要時採用的場景代碼（可空＝沿用會議場景）")]
    public string? SummarizeScenarioCode { get; set; }

    [DisplayName("建立人員")]
    [Comment("上傳人員")]
    public string CreatedBy { get; set; } = string.Empty;

    [DisplayName("建立時間")]
    [Comment("上傳時間")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [DisplayName("最後異動人員")]
    [Comment("最後異動人員")]
    public string? UpdatedBy { get; set; }

    [DisplayName("最後異動時間")]
    [Comment("最後異動時間")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Meeting? Meeting { get; set; }
}

[DisplayName("會議紀要")]
[Comment("會議紀要版本：每次產生新增一版，保留歷史")]
public class MeetingMinutes
{
    [DisplayName("識別主鍵")]
    [Comment("識別主鍵")]
    public Guid Id { get; set; }

    [DisplayName("會議")]
    [Comment("所屬會議")]
    public Guid MeetingId { get; set; }

    [DisplayName("版本")]
    [Comment("紀要版本（從 1 起）")]
    public int Version { get; set; }

    [DisplayName("場景代碼")]
    [Comment("產生時採用的場景代碼")]
    public string ScenarioCode { get; set; } = string.Empty;

    [DisplayName("紀要內容")]
    [Comment("Markdown 紀要")]
    public string Markdown { get; set; } = string.Empty;

    [DisplayName("模型")]
    [Comment("產生紀要的模型名稱")]
    public string Model { get; set; } = string.Empty;

    [DisplayName("產生時間")]
    [Comment("產生時間（UTC）")]
    public DateTimeOffset GeneratedAtUtc { get; set; }

    [DisplayName("產生者")]
    [Comment("觸發產生的使用者")]
    public string GeneratedBy { get; set; } = string.Empty;

    public Meeting? Meeting { get; set; }
}

[DisplayName("場景 Prompt 模板")]
[Comment("會議場景的 Map / Reduce 提示詞模板")]
public class PromptTemplate : IAuditable
{
    [DisplayName("識別主鍵")]
    [Comment("識別主鍵")]
    public Guid Id { get; set; }

    [DisplayName("場景代碼")]
    [Comment("場景代碼（唯一，小寫英數與連字號）")]
    public string ScenarioCode { get; set; } = string.Empty;

    [DisplayName("顯示名稱")]
    [Comment("場景顯示名稱")]
    public string DisplayName { get; set; } = string.Empty;

    [DisplayName("Map 指令")]
    [Comment("Map 階段：自逐字稿片段提取重點的指令")]
    public string MapPrompt { get; set; } = string.Empty;

    [DisplayName("Reduce 骨架")]
    [Comment("Reduce 階段：最終 Markdown 輸出骨架與整合指令")]
    public string ReducePrompt { get; set; } = string.Empty;

    [DisplayName("是否啟用")]
    [Comment("停用後不可被選用")]
    public bool IsActive { get; set; } = true;

    [DisplayName("版本")]
    [Comment("每次修改遞增")]
    public int Version { get; set; } = 1;

    [DisplayName("建立人員")]
    [Comment("建立人員")]
    public string CreatedBy { get; set; } = string.Empty;

    [DisplayName("建立時間")]
    [Comment("建立時間")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [DisplayName("最後異動人員")]
    [Comment("最後異動人員")]
    public string? UpdatedBy { get; set; }

    [DisplayName("最後異動時間")]
    [Comment("最後異動時間")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

[DisplayName("術語表")]
[Comment("企業術語與同音誤聽校正表")]
public class GlossaryEntry : IAuditable
{
    [DisplayName("識別主鍵")]
    [Comment("識別主鍵")]
    public Guid Id { get; set; }

    [DisplayName("標準詞")]
    [Comment("正確的標準寫法（例如 Kubernetes）")]
    public string Term { get; set; } = string.Empty;

    [DisplayName("誤聽詞")]
    [Comment("常見誤聽或寫法，以 | 分隔（例如 酷伯|k8s）")]
    public string? Variants { get; set; }

    [DisplayName("類別")]
    [Comment("分類（可空）")]
    public string? Category { get; set; }

    [DisplayName("是否啟用")]
    [Comment("停用後不參與校正")]
    public bool IsActive { get; set; } = true;

    [DisplayName("建立人員")]
    [Comment("建立人員")]
    public string CreatedBy { get; set; } = string.Empty;

    [DisplayName("建立時間")]
    [Comment("建立時間")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [DisplayName("最後異動人員")]
    [Comment("最後異動人員")]
    public string? UpdatedBy { get; set; }

    [DisplayName("最後異動時間")]
    [Comment("最後異動時間")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

/// <summary>待辦事項狀態。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActionItemStatus
{
    Open = 0,
    InProgress = 1,
    Done = 2,
    Cancelled = 3
}

/// <summary>待辦事項來源：由紀要抽取，或人工新增。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActionItemSource
{
    Extracted = 0,
    Manual = 1
}

[DisplayName("會議待辦")]
[Comment("會議待辦事項：由紀要自動抽取或人工新增，可指派負責人與期限，並以郵件通知")]
public class ActionItem : IAuditable
{
    [DisplayName("識別主鍵")]
    [Comment("識別主鍵")]
    public Guid Id { get; set; }

    [DisplayName("會議")]
    [Comment("所屬會議")]
    public Guid MeetingId { get; set; }

    [DisplayName("序號")]
    [Comment("會議內顯示順序（從 1 起）")]
    public int Seq { get; set; }

    [DisplayName("待辦事項")]
    [Comment("待辦內容")]
    public string Description { get; set; } = string.Empty;

    [DisplayName("負責人")]
    [Comment("負責人姓名（文字；紀要未提及時為空）")]
    public string? Owner { get; set; }

    [Encrypted]
    [DisplayName("負責人信箱")]
    [Comment("負責人 Email（個資，落地加密；通知郵件的收件人）")]
    public string? OwnerEmail { get; set; }

    [DisplayName("期限")]
    [Comment("期限日期（能從紀要文字解析成日期時才有）")]
    public DateOnly? DueDate { get; set; }

    [DisplayName("期限原文")]
    [Comment("紀要中的期限描述（例如「第三周」「下週五前」），無法解析成日期時保留供人工判讀")]
    public string? DueText { get; set; }

    [DisplayName("狀態")]
    [Comment("Open、InProgress、Done、Cancelled")]
    public ActionItemStatus Status { get; set; } = ActionItemStatus.Open;

    [DisplayName("備註")]
    [Comment("備註")]
    public string? Note { get; set; }

    [DisplayName("來源")]
    [Comment("Extracted（由紀要抽取）、Manual（人工新增）")]
    public ActionItemSource Source { get; set; } = ActionItemSource.Extracted;

    [DisplayName("來源紀要版本")]
    [Comment("抽取自哪一版紀要（人工新增為空）")]
    public int? MinutesVersion { get; set; }

    [DisplayName("PMS 任務")]
    [Comment("已回寫為 PMS 專案 WBS 任務時的任務識別碼；未回寫為空。回寫後此待辦視為已處理，重新抽取不會刪除")]
    public Guid? PmsTaskId { get; set; }

    [DisplayName("PMS 回寫時間")]
    [Comment("回寫為 PMS 任務的時間（UTC）")]
    public DateTimeOffset? PmsSyncedAtUtc { get; set; }

    [DisplayName("最後通知時間")]
    [Comment("最近一次寄出通知郵件的時間（UTC）；未通知為空")]
    public DateTimeOffset? NotifiedAtUtc { get; set; }

    [DisplayName("建立人員")]
    [Comment("建立人員")]
    public string CreatedBy { get; set; } = string.Empty;

    [DisplayName("建立時間")]
    [Comment("建立時間")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [DisplayName("最後異動人員")]
    [Comment("最後異動人員")]
    public string? UpdatedBy { get; set; }

    [DisplayName("最後異動時間")]
    [Comment("最後異動時間；有值代表曾被人工修改（重新抽取時不會覆蓋）")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Meeting? Meeting { get; set; }
}
