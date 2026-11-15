using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.CAL.Infrastructure.Entities;

/// <summary>日曆類型：全公司共用、部門、專案。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CalendarType : byte
{
    /// <summary>全公司共用日曆（系統唯一一份，由人資／行政維護）。</summary>
    Company = 0,

    /// <summary>部門日曆（第二期）。</summary>
    Department = 1,

    /// <summary>專案日曆（第二期；可綁定 PMS 專案）。</summary>
    Project = 2
}

/// <summary>活動種類：一般活動、里程碑（由 PMS 等模組單向推送，第二期）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CalendarEventKind : byte
{
    Event = 0,
    Milestone = 1
}

/// <summary>假日資料來源：人工維護，或由政府行政機關辦公日曆同步（同步只會覆寫、刪除自己建立的資料）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HolidaySource : byte
{
    Manual = 0,
    GovSync = 1
}

/// <summary>政府辦公日曆同步的執行方式。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HolidaySyncMode : byte
{
    Manual = 0,
    Scheduled = 1
}

/// <summary>固定識別碼：全公司日曆由系統自動建立。</summary>
public static class CalendarIds
{
    public static readonly Guid Company = new("00000000-0000-0000-0000-0000000ca110");

    public const string CompanyCode = "COMPANY";
}

/// <summary>
/// 國定假日／補班日（公司工作日曆的例外）：工作日運算的唯一來源，PMS 的 SLA、合約工作日、加班判定等經 Core 的
/// <c>IWorkCalendarProvider</c> 讀取。未列出的日期依週一至週五為工作日。
/// </summary>
[DisplayName("假日與補班日")]
[Comment("公司工作日曆例外：放假（國定假日、公司休假）與補班日")]
public class Holiday : IAuditable
{
    [Comment("日期（主鍵）")]
    public DateOnly HolidayDate { get; set; }

    [Comment("名稱（例：中秋節、補班）")]
    public string Name { get; set; } = string.Empty;

    [Comment("false＝放假；true＝補班（週末上班）")]
    public bool IsWorkday { get; set; }

    [Comment("資料來源：0 人工維護；1 政府辦公日曆同步（同步只會覆寫與移除自己建立的資料）")]
    public HolidaySource Source { get; set; } = HolidaySource.Manual;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

/// <summary>日曆（全公司、部門、專案）。第一期只有一份自動建立的全公司日曆。</summary>
[DisplayName("日曆")]
[Comment("日曆主檔：全公司共用、部門、專案日曆")]
public class Calendar : IAuditable
{
    public Guid Id { get; set; }

    [Comment("日曆代碼（唯一；全公司為 COMPANY）")]
    public string Code { get; set; } = string.Empty;

    [Comment("日曆名稱")]
    public string Name { get; set; } = string.Empty;

    public CalendarType Type { get; set; }

    [Comment("顯示顏色（#RRGGBB）")]
    public string Color { get; set; } = "#0F6CBD";

    [Comment("說明")]
    public string? Description { get; set; }

    [Comment("已封存（專案結束或部門解散；封存後唯讀、預設不顯示）")]
    public bool IsArchived { get; set; }

    [Comment("專案日曆綁定的 PMS 專案識別碼（第二期；不建外鍵，避免跨 schema 相依）")]
    public Guid? ProjectId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public List<CalendarEvent> Events { get; set; } = [];
}

/// <summary>日曆活動（公司大會、教育訓練、節慶活動、專案里程碑…）。</summary>
[DisplayName("日曆活動")]
[Comment("日曆活動：標題、起迄、地點與說明")]
public class CalendarEvent : IAuditable
{
    public Guid Id { get; set; }
    public Guid CalendarId { get; set; }

    [Comment("標題（建議格式：[部門/專案] 會議主題 - 主持人）")]
    public string Title { get; set; } = string.Empty;

    public CalendarEventKind Kind { get; set; }

    [Comment("全天活動：起為當日 00:00、迄為最後一天的隔日 00:00（台灣時間）")]
    public bool AllDay { get; set; }

    [Comment("開始時間（含時區）")]
    public DateTimeOffset StartAt { get; set; }

    [Comment("結束時間（含時區，不含）")]
    public DateTimeOffset EndAt { get; set; }

    [Comment("地點或線上會議連結")]
    public string? Location { get; set; }

    [Comment("說明（議程、會前文件連結）")]
    public string? Description { get; set; }

    [Comment("分類（公司大會、教育訓練、節慶活動…）")]
    public string? Category { get; set; }

    [Comment("外部來源（例：PMS）；由其他模組推送的活動使用，與 ExternalKey 構成冪等鍵")]
    public string? ExternalSource { get; set; }

    [Comment("外部來源的活動識別（例：PMS 甘特圖任務 Id）")]
    public string? ExternalKey { get; set; }

    [Comment("建立者使用者識別碼（提醒信收件人）；系統同步的活動為空")]
    public Guid? CreatedByUserId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Calendar? Calendar { get; set; }

    public List<CalendarEventAttendee> Attendees { get; set; } = [];

    public List<CalendarEventReminder> Reminders { get; set; } = [];
}

/// <summary>活動與會者（提醒信收件人）。</summary>
[DisplayName("日曆活動與會者")]
[Comment("活動與會者：提醒信的收件人之一（另含活動建立者）")]
public class CalendarEventAttendee
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    [Comment("與會者使用者識別碼")]
    public Guid UserId { get; set; }

    public CalendarEvent? Event { get; set; }
}

/// <summary>活動提醒設定：活動開始前 N 分鐘寄出 Email 提醒（每組只寄一次）。</summary>
[DisplayName("日曆活動提醒")]
[Comment("活動提醒：開始前 N 分鐘寄 Email，寄出後記錄時間避免重複")]
public class CalendarEventReminder
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    [Comment("活動開始前幾分鐘提醒（1~20160）")]
    public int MinutesBefore { get; set; }

    [Comment("提醒信寄出時間；空表示尚未寄出（活動改期會重設）")]
    public DateTimeOffset? SentAtUtc { get; set; }

    public CalendarEvent? Event { get; set; }
}

/// <summary>政府行政機關辦公日曆同步紀錄（每次實際寫入或失敗各一筆，供稽核與排程追蹤）。</summary>
[DisplayName("假日同步紀錄")]
[Comment("政府辦公日曆同步紀錄：年度、方式、來源網址與新增／更新／移除筆數")]
public class HolidaySyncRun
{
    public Guid Id { get; set; }

    [Comment("同步的西元年度")]
    public int Year { get; set; }

    public HolidaySyncMode Mode { get; set; }

    [Comment("執行者（使用者或排程帳號）")]
    public string Actor { get; set; } = string.Empty;

    [Comment("資料來源網址")]
    public string SourceUrl { get; set; } = string.Empty;

    [Comment("是否成功")]
    public bool Succeeded { get; set; }

    public int Added { get; set; }

    public int Updated { get; set; }

    public int Removed { get; set; }

    [Comment("因人工維護而略過的筆數（排程不覆寫人工資料）")]
    public int SkippedManual { get; set; }

    [Comment("結果說明或失敗原因")]
    public string? Message { get; set; }

    public DateTimeOffset RunAtUtc { get; set; }
}
