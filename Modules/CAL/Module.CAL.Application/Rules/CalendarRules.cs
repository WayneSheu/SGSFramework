using System.Globalization;
using System.Text.RegularExpressions;
using SGS.Modules.CAL.Application.Contracts;

namespace SGS.Modules.CAL.Application.Rules;

/// <summary>假日與補班日的驗證規則（純函式）。</summary>
public static class HolidayRules
{
    public const int MaxBatch = 400;
    public const int MaxNameLength = 50;
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public static string? ValidateItem(HolidayDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > MaxNameLength)
            return $"{item.Date:yyyy-MM-dd} 的名稱為必填且不得超過 {MaxNameLength} 字。";
        if (item.Date.Year is < MinYear or > MaxYear)
            return $"日期 {item.Date:yyyy-MM-dd} 不在合理範圍（{MinYear}~{MaxYear} 年）。";
        return null;
    }

    public static string? ValidateBatch(IReadOnlyList<HolidayDto>? items)
    {
        if (items is null || items.Count == 0) return "請至少提供一筆假日。";
        if (items.Count > MaxBatch) return $"單次最多 {MaxBatch} 筆。";

        foreach (var item in items)
        {
            var invalid = ValidateItem(item);
            if (invalid is not null) return invalid;
        }

        return null;
    }

    /// <summary>同批次重複日期以最後一筆為準。</summary>
    public static IReadOnlyList<HolidayDto> Dedupe(IEnumerable<HolidayDto> items) =>
        items.GroupBy(i => i.Date).Select(g => g.Last()).OrderBy(i => i.Date).ToList();
}

/// <summary>CSV／Tab 分隔文字的解析結果。</summary>
public sealed record HolidayCsvRow(int Line, HolidayDto Item);

public sealed record HolidayCsvResult(IReadOnlyList<HolidayCsvRow> Rows, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

/// <summary>
/// 假日批次匯入文字解析（純函式）。格式：每行「日期,名稱,類型」，逗號或 Tab 分隔（可直接貼上 Excel）；
/// 日期支援 yyyy-MM-dd、yyyy/M/d、yyyyMMdd；類型可省略（預設放假），「放假／假日／holiday」或「補班／上班／makeup／workday」；
/// 第一行若是標題（含「日期」或 date）自動略過；空白行與 # 開頭的註解行略過。
/// </summary>
public static class HolidayCsv
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/M/d", "yyyy-M-d", "yyyy/MM/dd", "yyyyMMdd"];

    private static readonly string[] HolidayWords = ["放假", "假日", "休假", "國定假日", "holiday", "h", "false", "0"];

    private static readonly string[] WorkdayWords = ["補班", "補班日", "上班", "補行上班", "makeup", "workday", "w", "true", "1"];

    public static HolidayCsvResult Parse(string? text)
    {
        var rows = new List<HolidayCsvRow>();
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(text)) return new HolidayCsvResult(rows, ["沒有可匯入的內容。"], warnings);

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var dataLines = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNo = i + 1;
            var line = lines[i].Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var parts = line.Split(line.Contains('\t') ? '\t' : ',').Select(p => p.Trim().Trim('"').Trim()).ToArray();

            // 標題列：第一個欄位不是日期、且含「日期」或 date
            if (dataLines == 0 && !TryParseDate(parts[0], out _) && Regex.IsMatch(parts[0], "日期|date", RegexOptions.IgnoreCase))
                continue;

            dataLines++;
            if (dataLines > HolidayRules.MaxBatch)
            {
                errors.Add($"最多匯入 {HolidayRules.MaxBatch} 筆，第 {lineNo} 行起不處理。");
                break;
            }

            if (parts.Length < 2) { errors.Add($"第 {lineNo} 行：至少需要「日期,名稱」兩個欄位。"); continue; }
            if (!TryParseDate(parts[0], out var date)) { errors.Add($"第 {lineNo} 行：日期「{parts[0]}」格式不正確（請用 2026-10-10 或 2026/10/10）。"); continue; }

            var name = parts[1];
            var isWorkday = false;
            if (parts.Length >= 3 && parts[2].Length > 0)
            {
                if (HolidayWords.Contains(parts[2], StringComparer.OrdinalIgnoreCase)) isWorkday = false;
                else if (WorkdayWords.Contains(parts[2], StringComparer.OrdinalIgnoreCase)) isWorkday = true;
                else { errors.Add($"第 {lineNo} 行：類型「{parts[2]}」不認得（請用 放假 或 補班）。"); continue; }
            }

            var item = new HolidayDto(date, name, isWorkday);
            var invalid = HolidayRules.ValidateItem(item);
            if (invalid is not null) { errors.Add($"第 {lineNo} 行：{invalid}"); continue; }

            var duplicate = rows.FindIndex(r => r.Item.Date == date);
            if (duplicate >= 0)
            {
                warnings.Add($"第 {rows[duplicate].Line} 行與第 {lineNo} 行日期重複（{date:yyyy-MM-dd}），以後者為準。");
                rows.RemoveAt(duplicate);
            }

            rows.Add(new HolidayCsvRow(lineNo, item with { Name = name.Trim() }));
        }

        if (rows.Count == 0 && errors.Count == 0) errors.Add("沒有可匯入的內容。");
        return new HolidayCsvResult(rows.OrderBy(r => r.Item.Date).ToList(), errors, warnings);
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}

/// <summary>日曆活動的驗證與命名規範（純函式）。</summary>
public static partial class CalendarEventRules
{
    public const int MaxTitleLength = 200;
    public const int MaxSpanDays = 31;
    public const int MaxViewDays = 62;

    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

    /// <summary>顏色格式 #RRGGBB。</summary>
    public static bool IsValidColor(string? color) => color is not null && ColorRegex().IsMatch(color);

    /// <summary>
    /// 標題是否符合「[部門/專案] 會議主題 - 主持人」命名建議（只是提示，不阻擋儲存）。
    /// </summary>
    public static bool IsNamingConventional(string? title) => !string.IsNullOrWhiteSpace(title) && NamingRegex().IsMatch(title.Trim());

    public static string? Validate(UpsertCalendarEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > MaxTitleLength) return $"標題必填且不得超過 {MaxTitleLength} 字。";
        if (!Enum.IsDefined(request.Kind)) return "活動種類不正確。";
        if (request.Location is { Length: > 200 }) return "地點不得超過 200 字。";
        if (request.Description is { Length: > 2000 }) return "說明不得超過 2000 字。";
        if (request.Category is { Length: > 50 }) return "分類不得超過 50 字。";

        var start = request.StartAt.ToOffset(TaipeiOffset);
        var end = request.EndAt.ToOffset(TaipeiOffset);
        if (end <= start) return "結束時間須晚於開始時間。";
        if (end - start > TimeSpan.FromDays(MaxSpanDays)) return $"單一活動最長 {MaxSpanDays} 天。";
        if (start.Year is < 2000 or > 2100) return "開始日期不在合理範圍。";

        if (request.AllDay && (start.TimeOfDay != TimeSpan.Zero || end.TimeOfDay != TimeSpan.Zero))
            return "全天活動的起訖須為 00:00（結束為最後一天的隔日 00:00）。";

        return null;
    }

    /// <summary>檢視區間驗證：起不得晚於迄，最長 62 天（單一月曆畫面）。</summary>
    public static string? ValidateViewRange(DateOnly from, DateOnly to)
    {
        if (to < from) return "結束日期不得早於開始日期。";
        if (to.DayNumber - from.DayNumber + 1 > MaxViewDays) return $"查詢區間最長 {MaxViewDays} 天。";
        if (from.Year is < 2000 or > 2100 || to.Year is < 2000 or > 2100) return "日期不在合理範圍。";
        return null;
    }

    /// <summary>台灣日期區間 [from, to] 轉為含時區的查詢上下界：[from 00:00, to+1 00:00)。</summary>
    public static (DateTimeOffset Start, DateTimeOffset EndExclusive) ToRange(DateOnly from, DateOnly to) =>
        (new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TaipeiOffset), new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TaipeiOffset));

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorRegex();

    [GeneratedRegex(@"^\[[^\]]+\]\s*\S.*\s-\s\S.*$")]
    private static partial Regex NamingRegex();
}
