using System.Globalization;
using System.Text.Json;
using SGS.Modules.CAL.Application.Contracts;
using SGS.Modules.CAL.Infrastructure.Entities;

namespace SGS.Modules.CAL.Application.Rules;

/// <summary>政府辦公日曆的一天：是否放假與備註（國定假日名稱、補假、調整放假、補行上班等）。</summary>
public sealed record GovDay(DateOnly Date, bool IsOff, string Description);

public sealed record GovCalendarParseResult(IReadOnlyList<GovDay> Days, IReadOnlyList<string> Errors);

/// <summary>
/// 中華民國政府行政機關辦公日曆表（行政院人事行政總處）的解析與轉換（純函式）。
/// 支援兩種格式：
/// ①官方開放資料 CSV：欄位「西元日期(yyyyMMdd), 星期, 是否放假(0＝上班、2＝放假), 備註」；
/// ②JSON（如 TaiwanCalendar 專案，由官方資料轉出）：<c>[{"date":"20260101","week":"四","isHoliday":true,"description":"開國紀念日"}]</c>。
/// 來源是「全年每一天」；本系統只存「與預設不同的日子」（週一至週五上班、週六日休息）。
/// </summary>
public static class GovCalendarRules
{
    /// <summary>全年資料至少要有的天數（含閏年 366 天；少於此視為來源不完整，避免半份資料覆寫既有假日）。</summary>
    public const int MinDaysPerYear = 360;

    public static GovCalendarParseResult Parse(string? text, int year)
    {
        var errors = new List<string>();
        var days = new List<GovDay>();

        if (string.IsNullOrWhiteSpace(text)) return new GovCalendarParseResult(days, ["來源內容是空的。"]);

        var trimmed = text.TrimStart('﻿', ' ', '\r', '\n', '\t');
        if (trimmed.StartsWith('[')) ParseJson(trimmed, days, errors);
        else ParseCsv(text, days, errors);

        if (errors.Count > 0) return new GovCalendarParseResult([], errors);

        var inYear = days.Where(d => d.Date.Year == year).GroupBy(d => d.Date).Select(g => g.Last()).OrderBy(d => d.Date).ToList();
        if (inYear.Count == 0) return new GovCalendarParseResult([], [$"來源沒有 {year} 年的資料。"]);
        if (inYear.Count < MinDaysPerYear)
            return new GovCalendarParseResult([], [$"來源 {year} 年只有 {inYear.Count} 天資料（至少需 {MinDaysPerYear} 天），可能尚未完整公布，未同步。"]);

        return new GovCalendarParseResult(inYear, errors);
    }

    private static void ParseJson(string text, List<GovDay> days, List<string> errors)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) { errors.Add("JSON 內容須為陣列。"); return; }

            var index = 0;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                index++;
                var date = item.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
                if (!TryParseDate(date, out var parsed)) { errors.Add($"第 {index} 筆：日期「{date}」格式不正確。"); continue; }
                if (!item.TryGetProperty("isHoliday", out var h) || h.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    errors.Add($"第 {index} 筆（{parsed:yyyy-MM-dd}）：缺少 isHoliday。");
                    continue;
                }

                var description = item.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String ? desc.GetString() ?? string.Empty : string.Empty;
                days.Add(new GovDay(parsed, h.GetBoolean(), description.Trim()));
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"JSON 格式不正確：{ex.Message}");
        }
    }

    private static void ParseCsv(string text, List<GovDay> days, List<string> errors)
    {
        var lineNo = 0;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            lineNo++;
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0) continue;

            var parts = SplitCsv(line);
            if (parts.Count < 3) { if (days.Count == 0 && lineNo == 1) continue; errors.Add($"第 {lineNo} 行：欄位不足（需 西元日期,星期,是否放假,備註）。"); continue; }

            // 標題列：第一欄不是日期（例：西元日期）
            if (!TryParseDate(parts[0], out var date))
            {
                if (days.Count == 0 && lineNo <= 2) continue;
                errors.Add($"第 {lineNo} 行：日期「{parts[0]}」格式不正確。");
                continue;
            }

            var flag = parts[2].Trim();
            bool isOff;
            if (flag == "2") isOff = true;
            else if (flag == "0") isOff = false;
            else { errors.Add($"第 {lineNo} 行：是否放假「{flag}」不認得（0＝上班、2＝放假）。"); continue; }

            days.Add(new GovDay(date, isOff, parts.Count > 3 ? parts[3].Trim() : string.Empty));
        }
    }

    /// <summary>CSV 一行拆欄（支援雙引號包住含逗號的備註）。</summary>
    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else current.Append(c);
        }

        result.Add(current.ToString().Trim());
        return result;
    }

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value?.Trim(), ["yyyyMMdd", "yyyy-MM-dd", "yyyy/M/d", "yyyy/MM/dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>
    /// 全年資料 → 「與預設不同」的日子：
    /// 週一至週五放假（國定假日、補假、調整放假）→ 放假；週六日上班（補行上班）→ 補班；
    /// 週六日休息但有備註（國定假日恰逢週末，例如國慶日在週六）→ 放假（保留名稱，加班倍率判定需要它是國定假日）；
    /// 其餘（一般上班日、沒有備註的週末）不存。名稱超過 50 字會截斷。
    /// </summary>
    public static IReadOnlyList<HolidayDto> DeriveOverrides(IEnumerable<GovDay> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        var result = new List<HolidayDto>();
        foreach (var day in days)
        {
            var weekend = day.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var description = day.Description.Trim();

            if (day.IsOff && !weekend) result.Add(new HolidayDto(day.Date, Name(description, "放假"), false));
            else if (day.IsOff && weekend && description.Length > 0) result.Add(new HolidayDto(day.Date, Name(description, "放假"), false));
            else if (!day.IsOff && weekend) result.Add(new HolidayDto(day.Date, Name(description, "補班"), true));
        }

        return result.OrderBy(r => r.Date).ToList();
    }

    private static string Name(string description, string fallback)
    {
        var name = description.Length == 0 ? fallback : description;
        return name.Length > HolidayRules.MaxNameLength ? name[..HolidayRules.MaxNameLength] : name;
    }

    /// <summary>現有資料（日期 → 名稱、類型、來源）。</summary>
    public sealed record ExistingHoliday(string Name, bool IsWorkday, HolidaySource Source);

    /// <summary>單日差異。</summary>
    public sealed record SyncItem(DateOnly Date, string Name, bool IsWorkday, GovSyncAction Action, string? Previous);

    public sealed record SyncPlan(IReadOnlyList<SyncItem> Items, int Added, int Updated, int Removed, int Unchanged, int ManualConflicts);

    /// <summary>
    /// 比對政府資料與現有資料，產生同步計畫：
    /// 新增（現有沒有）、更新（內容不同；現有為人工資料時視為「人工衝突」——<paramref name="overwriteManual"/> 為真才覆寫，排程一律保留人工資料）、
    /// 移除（現有為同步資料、但政府資料已不再有此例外日；人工資料永不移除）、不變。
    /// </summary>
    public static SyncPlan Plan(int year, IReadOnlyList<HolidayDto> derived, IReadOnlyDictionary<DateOnly, ExistingHoliday> existingInYear, bool overwriteManual)
    {
        ArgumentNullException.ThrowIfNull(derived);
        ArgumentNullException.ThrowIfNull(existingInYear);

        var items = new List<SyncItem>();
        int added = 0, updated = 0, removed = 0, unchanged = 0, conflicts = 0;

        foreach (var item in derived)
        {
            if (!existingInYear.TryGetValue(item.Date, out var current))
            {
                items.Add(new SyncItem(item.Date, item.Name, item.IsWorkday, GovSyncAction.Add, null));
                added++;
                continue;
            }

            if (current.IsWorkday == item.IsWorkday && current.Name == item.Name)
            {
                items.Add(new SyncItem(item.Date, item.Name, item.IsWorkday, GovSyncAction.Unchanged, null));
                unchanged++;
                continue;
            }

            var previous = $"{(current.IsWorkday ? "補班" : "放假")}「{current.Name}」";
            if (current.Source == HolidaySource.Manual)
            {
                conflicts++;
                if (overwriteManual)
                {
                    items.Add(new SyncItem(item.Date, item.Name, item.IsWorkday, GovSyncAction.Update, previous + "（人工維護）"));
                    updated++;
                }
                else
                {
                    items.Add(new SyncItem(item.Date, item.Name, item.IsWorkday, GovSyncAction.SkipManual, previous + "（人工維護，保留）"));
                }

                continue;
            }

            items.Add(new SyncItem(item.Date, item.Name, item.IsWorkday, GovSyncAction.Update, previous));
            updated++;
        }

        var derivedDates = derived.Select(d => d.Date).ToHashSet();
        foreach (var (date, current) in existingInYear.Where(e => e.Key.Year == year && e.Value.Source == HolidaySource.GovSync && !derivedDates.Contains(e.Key)).OrderBy(e => e.Key))
        {
            items.Add(new SyncItem(date, current.Name, current.IsWorkday, GovSyncAction.Remove, $"{(current.IsWorkday ? "補班" : "放假")}「{current.Name}」（政府資料已無此日）"));
            removed++;
        }

        return new SyncPlan(items.OrderBy(i => i.Date).ToList(), added, updated, removed, unchanged, conflicts);
    }

    /// <summary>來源網址樣板：{year} 西元年、{rocYear} 民國年（例：115）。</summary>
    public static string? ResolveUrl(string? template, int year)
    {
        if (string.IsNullOrWhiteSpace(template)) return null;

        var url = template.Replace("{year}", year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{rocYear}", (year - 1911).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri.ToString() : null;
    }

    /// <summary>可同步的年度範圍：去年到明年（避免誤改更早的歷史資料）。</summary>
    public static string? ValidateYear(int year, int currentYear) =>
        year < currentYear - 1 || year > currentYear + 1 ? $"只能同步 {currentYear - 1}～{currentYear + 1} 年。" : null;
}

/// <summary>排程自動同步要涵蓋的年度：當年與明年（明年的辦公日曆通常在前一年中就會公布）。</summary>
public static class GovSyncSchedule
{
    public static IReadOnlyList<int> Years(int currentYear) => [currentYear, currentYear + 1];
}
