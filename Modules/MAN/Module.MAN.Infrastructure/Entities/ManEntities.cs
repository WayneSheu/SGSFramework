using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.MAN.Infrastructure.Entities;

/// <summary>操作手冊主題狀態：草稿（可含 AI 草稿）、已發布（Tooltip 才會對全站生效）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ManualTopicStatus : byte
{
    Draft = 0,
    Published = 1
}

/// <summary>內容來源：人工撰寫，或由本地 Ollama 生成的 AI 草稿（仍須人工審核後才可發布）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ManualContentSource : byte
{
    Manual = 0,
    AiGenerated = 1
}

/// <summary>
/// 操作手冊主題：一個功能（FeatureKey）對應一份內嵌 Tooltip、操作步驟與注意事項；
/// 同一份內容輸出為軟體內嵌提示、網頁說明中心與 PDF 手冊（單一來源多格式發布）。
/// </summary>
[DisplayName("操作手冊主題")]
[Comment("操作手冊主題：Tooltip、操作步驟與注意事項的單一來源")]
public class ManualTopic : IAuditable
{
    public Guid Id { get; set; }

    [Comment("功能代碼（唯一；前端 SmartTooltip 以此綁定，例如 export_finance_report）")]
    public string FeatureKey { get; set; } = string.Empty;

    [Comment("主題標題")]
    public string Title { get; set; } = string.Empty;

    [Comment("所屬模組／章節名稱（用於手冊分章）")]
    public string? ModuleName { get; set; }

    [Comment("內嵌提示（30 字以內）")]
    public string Tooltip { get; set; } = string.Empty;

    [Comment("操作步驟（Markdown）")]
    public string StepsMarkdown { get; set; } = string.Empty;

    [Comment("注意事項（Markdown）")]
    public string Precautions { get; set; } = string.Empty;

    [Comment("手冊排序")]
    public int SortOrder { get; set; }

    [Comment("0 草稿；1 已發布")]
    public ManualTopicStatus Status { get; set; } = ManualTopicStatus.Draft;

    [Comment("0 人工；1 AI 生成草稿")]
    public ManualContentSource Source { get; set; } = ManualContentSource.Manual;

    public DateTimeOffset? PublishedAtUtc { get; set; }
    public string? PublishedBy { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

/// <summary>AI 生成作業狀態。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GenerationJobStatus : byte
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

/// <summary>
/// AI 生成作業：截圖只在記憶體佇列中處理，不落地、不寫入資料庫（含畫面個資的風險最小化）；
/// 此表僅記錄作業狀態、使用模型、耗時與結果主題，供追蹤與稽核。
/// </summary>
[DisplayName("AI 生成作業")]
[Comment("操作手冊 AI 生成作業紀錄（不含截圖內容）")]
public class GenerationJob : IAuditable
{
    public Guid Id { get; set; }

    [Comment("功能代碼")]
    public string FeatureKey { get; set; } = string.Empty;

    [Comment("功能標題")]
    public string Title { get; set; } = string.Empty;

    [Comment("0 等待中；1 執行中；2 成功；3 失敗")]
    public GenerationJobStatus Status { get; set; } = GenerationJobStatus.Pending;

    [Comment("使用的視覺模型")]
    public string Model { get; set; } = string.Empty;

    [Comment("是否允許覆寫既有主題")]
    public bool Overwrite { get; set; }

    [Comment("生成結果對應的手冊主題（成功時）")]
    public Guid? TopicId { get; set; }

    [Comment("失敗原因或提示訊息（已去除模型原始輸出）")]
    public string? Message { get; set; }

    [Comment("執行耗時（毫秒）")]
    public long? DurationMs { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
