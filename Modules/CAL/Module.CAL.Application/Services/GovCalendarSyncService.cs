using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGS.Modules.CAL.Application.Contracts;
using SGS.Modules.CAL.Application.Rules;
using SGS.Modules.CAL.Infrastructure.Dbcontexts;
using SGS.Modules.CAL.Infrastructure.Entities;

namespace SGS.Modules.CAL.Application.Services;

/// <summary>
/// 政府行政機關辦公日曆（行政院人事行政總處）同步：下載指定年度的全年資料，轉成「與預設不同的日子」後比對現有假日。
/// 手動同步先預覽差異再寫入；排程每月自動同步當年與明年——只新增與更新「同步來源」的資料，人工維護且內容不同的日子一律保留，
/// 並且不會移除人工資料。每次實際寫入或失敗都留下同步紀錄。來源網址由 <c>Calendar:GovSync:UrlTemplate</c> 設定（https；{year} 西元年、{rocYear} 民國年）。
/// </summary>
public sealed class GovCalendarSyncService(
    CALDbContext db,
    HolidayCache cache,
    IHttpClientFactory httpClients,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<GovCalendarSyncService> logger)
{
    public const string HttpClientName = "cal-gov-calendar";

    /// <summary>預設來源：TaiwanCalendar 專案（由行政院人事行政總處公布的辦公日曆轉出的 JSON），經 jsDelivr CDN 提供；可於組態改成官方 CSV 位置。</summary>
    public const string DefaultUrlTemplate = "https://cdn.jsdelivr.net/gh/ruyut/TaiwanCalendar/data/{year}.json";

    private const int MaxBytes = 2 * 1024 * 1024;

    private const string ScheduledActor = "系統排程（cal-holiday-sync）";

    private string UrlTemplate => configuration["Calendar:GovSync:UrlTemplate"] is { Length: > 0 } configured ? configured : DefaultUrlTemplate;

    public async Task<IReadOnlyList<HolidaySyncRunDto>> RecentRunsAsync(int? top, CancellationToken ct)
    {
        var take = Math.Clamp(top ?? 10, 1, 50);
        return await db.HolidaySyncRuns.AsNoTracking().OrderByDescending(r => r.RunAtUtc).Take(take)
            .Select(r => new HolidaySyncRunDto(r.Id, r.Year, r.Mode, r.Actor, r.SourceUrl, r.Succeeded, r.Added, r.Updated, r.Removed, r.SkippedManual, r.Message, r.RunAtUtc))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>手動同步：Commit=false 預覽差異；Commit=true 寫入（OverwriteManual 為真才覆寫人工維護且內容不同的日子）。</summary>
    public async Task<CalResult<GovSyncResultDto>> SyncAsync(string actor, GovSyncRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var today = Today();
        var invalidYear = GovCalendarRules.ValidateYear(request.Year, today.Year);
        if (invalidYear is not null) return CalError.Validation(invalidYear);

        var outcome = await RunYearAsync(request.Year, request.Commit, request.OverwriteManual, HolidaySyncMode.Manual, actor, ct).ConfigureAwait(false);
        return outcome.Error is { } error ? error : CalResult<GovSyncResultDto>.Success(outcome.Result!);
    }

    /// <summary>排程自動同步：當年與明年；明年尚未公布（來源 404）不算失敗。永遠不覆寫人工維護的資料。</summary>
    public async Task<GovSyncScanResultDto> ScanAsync(CancellationToken ct)
    {
        int added = 0, updated = 0, removed = 0, skipped = 0, failed = 0, synced = 0;
        var notes = new List<string>();

        foreach (var year in GovSyncSchedule.Years(Today().Year))
        {
            var outcome = await RunYearAsync(year, commit: true, overwriteManual: false, HolidaySyncMode.Scheduled, ScheduledActor, ct).ConfigureAwait(false);
            if (outcome.Error is { } error)
            {
                if (error.Code == "CAL.GOV_NOT_PUBLISHED") { notes.Add($"{year} 年政府尚未公布"); continue; }

                failed++;
                notes.Add($"{year} 年失敗：{error.Detail}");
                continue;
            }

            var r = outcome.Result!;
            synced++;
            added += r.Added;
            updated += r.Updated;
            removed += r.Removed;
            skipped += r.ManualConflicts;
            notes.Add($"{year} 年新增 {r.Added}、更新 {r.Updated}、移除 {r.Removed}、保留人工 {r.ManualConflicts}");
        }

        var message = string.Join("；", notes);
        logger.LogInformation("[CAL] 政府辦公日曆自動同步：{Message}", message);
        return new GovSyncScanResultDto(synced, added, updated, removed, skipped, failed, message);
    }

    private sealed record YearOutcome(GovSyncResultDto? Result, CalError? Error);

    private async Task<YearOutcome> RunYearAsync(int year, bool commit, bool overwriteManual, HolidaySyncMode mode, string actor, CancellationToken ct)
    {
        var url = GovCalendarRules.ResolveUrl(UrlTemplate, year);
        if (url is null) return new YearOutcome(null, CalError.Validation("來源網址未設定或不是 https（Calendar:GovSync:UrlTemplate）。"));

        var fetched = await FetchAsync(url, ct).ConfigureAwait(false);
        if (fetched.Error is { } fetchError)
        {
            if (commit) await RecordAsync(year, mode, actor, url, false, 0, 0, 0, 0, fetchError.Detail, ct).ConfigureAwait(false);
            return new YearOutcome(null, fetchError);
        }

        var parsed = GovCalendarRules.Parse(fetched.Text, year);
        if (parsed.Errors.Count > 0)
        {
            var detail = string.Join("；", parsed.Errors.Take(3));
            if (commit) await RecordAsync(year, mode, actor, url, false, 0, 0, 0, 0, detail, ct).ConfigureAwait(false);
            return new YearOutcome(null, CalError.Validation($"來源資料無法使用：{detail}", "CAL.GOV_DATA_INVALID"));
        }

        var derived = GovCalendarRules.DeriveOverrides(parsed.Days);
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var existing = await db.Holidays.Where(h => h.HolidayDate >= from && h.HolidayDate <= to).ToDictionaryAsync(h => h.HolidayDate, ct).ConfigureAwait(false);
        var plan = GovCalendarRules.Plan(year, derived,
            existing.ToDictionary(e => e.Key, e => new GovCalendarRules.ExistingHoliday(e.Value.Name, e.Value.IsWorkday, e.Value.Source)), overwriteManual);

        var committed = false;
        if (commit)
        {
            var now = clock.GetUtcNow();
            foreach (var item in plan.Items)
            {
                switch (item.Action)
                {
                    case GovSyncAction.Add:
                        db.Holidays.Add(new Holiday
                        {
                            HolidayDate = item.Date, Name = item.Name, IsWorkday = item.IsWorkday, Source = HolidaySource.GovSync, CreatedBy = actor, CreatedAtUtc = now
                        });
                        break;
                    case GovSyncAction.Update:
                        var row = existing[item.Date];
                        row.Name = item.Name;
                        row.IsWorkday = item.IsWorkday;
                        row.Source = HolidaySource.GovSync;
                        row.UpdatedBy = actor;
                        row.UpdatedAtUtc = now;
                        break;
                    case GovSyncAction.Remove:
                        db.Holidays.Remove(existing[item.Date]);
                        break;
                }
            }

            db.HolidaySyncRuns.Add(new HolidaySyncRun
            {
                Id = Guid.NewGuid(), Year = year, Mode = mode, Actor = actor, SourceUrl = url, Succeeded = true, Added = plan.Added, Updated = plan.Updated,
                Removed = plan.Removed, SkippedManual = overwriteManual ? 0 : plan.ManualConflicts, RunAtUtc = now,
                Message = $"{year} 年共 {derived.Count} 個假日／補班日；不變 {plan.Unchanged}"
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            cache.Invalidate([year]);
            committed = true;
        }

        var items = plan.Items.Select(i => new GovSyncItemDto(i.Date, i.Name, i.IsWorkday, i.Action, i.Previous)).ToList();
        return new YearOutcome(new GovSyncResultDto(year, url, items, plan.Added, plan.Updated, plan.Removed, plan.Unchanged, plan.ManualConflicts, committed), null);
    }

    private sealed record Fetched(string? Text, CalError? Error);

    private async Task<Fetched> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            var client = httpClients.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(20);

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new Fetched(null, CalError.Conflict("CAL.GOV_NOT_PUBLISHED", "政府尚未公布該年度辦公日曆（來源回應 404）。"));
            if (!response.IsSuccessStatusCode)
                return new Fetched(null, CalError.Conflict("CAL.GOV_FETCH_FAILED", $"無法取得政府辦公日曆（來源回應 HTTP {(int)response.StatusCode}）。"));
            if (response.Content.Headers.ContentLength is > MaxBytes)
                return new Fetched(null, CalError.Validation("來源檔案過大，已拒絕。", "CAL.GOV_DATA_INVALID"));

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes) return new Fetched(null, CalError.Validation("來源檔案過大，已拒絕。", "CAL.GOV_DATA_INVALID"));
            }

            return new Fetched(System.Text.Encoding.UTF8.GetString(buffer.ToArray()), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "[CAL] 下載政府辦公日曆失敗：{Url}", url);
            return new Fetched(null, CalError.Conflict("CAL.GOV_FETCH_FAILED", "無法連線到政府辦公日曆來源（網路不通或逾時），請確認伺服器可連外網。"));
        }
    }

    private async Task RecordAsync(int year, HolidaySyncMode mode, string actor, string url, bool succeeded, int added, int updated, int removed, int skipped, string message, CancellationToken ct)
    {
        db.HolidaySyncRuns.Add(new HolidaySyncRun
        {
            Id = Guid.NewGuid(), Year = year, Mode = mode, Actor = actor, SourceUrl = url, Succeeded = succeeded, Added = added, Updated = updated, Removed = removed,
            SkippedManual = skipped, Message = message.Length > 1000 ? message[..1000] : message, RunAtUtc = clock.GetUtcNow()
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(8)).DateTime);
}
