using System.Text.Json.Serialization;
using SGS.Modules.CAL.Infrastructure.Entities;

namespace SGS.Modules.CAL.Application.Contracts;

/// <summary>CAL 業務錯誤（對應 HTTP 狀態與 code）。</summary>
public sealed record CalError(string Code, int Status, string Title, string Detail)
{
    public static CalError Validation(string detail, string code = "CAL.VALIDATION_FAILED") => new(code, 400, "Invalid Request", detail);

    /// <summary>找不到或無權限可見：一律 404，避免洩漏資源是否存在。</summary>
    public static CalError NotFound(string detail = "資源不存在。") => new("CAL.NOT_FOUND", 404, "Resource Not Found", detail);

    public static CalError Forbidden(string detail) => new("CAL.FORBIDDEN", 403, "Forbidden", detail);

    public static CalError Conflict(string code, string detail) => new(code, 409, "Conflict", detail);
}

/// <summary>操作結果（Result Pattern）。</summary>
public sealed class CalResult<T>
{
    private CalResult(T? value, CalError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public CalError? Error { get; }

    public bool IsSuccess => Error is null;

    public static CalResult<T> Success(T value) => new(value, null);

    public static CalResult<T> Failure(CalError error) => new(default, error);

    public static implicit operator CalResult<T>(CalError error) => Failure(error);
}

// ===== 假日與補班日 =====

/// <param name="IsWorkday">false＝放假；true＝補班（週末上班）。</param>
/// <param name="Source">資料來源：人工維護，或政府辦公日曆同步（同步只會覆寫與移除自己建立的資料）。</param>
public sealed record HolidayDto(DateOnly Date, string Name, bool IsWorkday, HolidaySource Source = HolidaySource.Manual);

public sealed record HolidaySearchRequest(int? Year);

public sealed record UpsertHolidaysRequest(IReadOnlyList<HolidayDto> Items);

public sealed record UpsertHolidaysResultDto(int Upserted);

/// <param name="Csv">貼上或上傳的 CSV／Tab 分隔文字（日期,名稱,類型）。</param>
/// <param name="Commit">false＝只預覽（不寫入）；true＝確認匯入。</param>
public sealed record HolidayImportRequest(string Csv, bool Commit);

public sealed record HolidayImportRowDto(int Line, DateOnly Date, string Name, bool IsWorkday, bool IsNew);

public sealed record HolidayImportResultDto(
    IReadOnlyList<HolidayImportRowDto> Rows, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, int NewCount, int UpdateCount, bool Committed);

// ===== 政府辦公日曆同步 =====

/// <summary>同步計畫中單日的動作。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GovSyncAction
{
    Add = 0,
    Update = 1,
    Remove = 2,
    Unchanged = 3,

    /// <summary>政府資料與人工維護的內容不同，維持人工資料（不覆寫）。</summary>
    SkipManual = 4
}

/// <param name="Year">西元年度（去年～明年）。</param>
/// <param name="Commit">false＝只預覽；true＝寫入。</param>
/// <param name="OverwriteManual">寫入時是否連人工維護且內容不同的日子一起覆寫（預覽會標出這類衝突）。</param>
public sealed record GovSyncRequest(int Year, bool Commit, bool OverwriteManual = false);

public sealed record GovSyncItemDto(DateOnly Date, string Name, bool IsWorkday, GovSyncAction Action, string? Previous);

public sealed record HolidaySyncRunDto(
    Guid Id, int Year, HolidaySyncMode Mode, string Actor, string SourceUrl, bool Succeeded, int Added, int Updated, int Removed, int SkippedManual,
    string? Message, DateTimeOffset RunAtUtc);

public sealed record GovSyncResultDto(
    int Year, string SourceUrl, IReadOnlyList<GovSyncItemDto> Items, int Added, int Updated, int Removed, int Unchanged, int ManualConflicts, bool Committed);

public sealed record GovSyncScanResultDto(int Years, int Added, int Updated, int Removed, int SkippedManual, int Failed, string Message);

public sealed record HolidaySyncRunSearchRequest(int? Top);

// ===== 日曆與活動 =====

public sealed record CalendarDto(Guid Id, string Code, string Name, CalendarType Type, string Color, bool IsArchived);

public sealed record CalendarEventDto(
    Guid Id, Guid CalendarId, string Title, CalendarEventKind Kind, bool AllDay, DateTimeOffset StartAt, DateTimeOffset EndAt,
    string? Location, string? Description, string? Category, string CreatedBy, bool NamingConventional, bool IsExternal,
    IReadOnlyList<Guid>? AttendeeUserIds = null, IReadOnlyList<int>? ReminderMinutes = null);

/// <param name="From">起日（含，台灣日期）。</param>
/// <param name="To">迄日（含，台灣日期）；區間最長 62 天。</param>
/// <param name="CalendarIds">只看指定日曆；空代表全部可見日曆。</param>
public sealed record CalendarViewRequest(DateOnly From, DateOnly To, IReadOnlyList<Guid>? CalendarIds = null);

public sealed record CalendarViewDto(
    IReadOnlyList<CalendarDto> Calendars, IReadOnlyList<CalendarEventDto> Events, IReadOnlyList<HolidayDto> Holidays);

public sealed record UpsertCalendarEventRequest(
    Guid CalendarId, string Title, CalendarEventKind Kind, bool AllDay, DateTimeOffset StartAt, DateTimeOffset EndAt,
    string? Location, string? Description, string? Category, IReadOnlyList<Guid>? AttendeeUserIds = null, IReadOnlyList<int>? ReminderMinutes = null);

// ===== 提醒派送 =====

public sealed record ReminderDispatchResultDto(int Sent, int NoRecipients, int Failed, string Message);
