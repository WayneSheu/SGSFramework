using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SGS.Modules.CAL.Application.Contracts;
using SGS.Modules.CAL.Application.Rules;
using SGS.Modules.CAL.Infrastructure.Dbcontexts;
using SGS.Modules.CAL.Infrastructure.Entities;
using SGSFramework.Core.Calendars;

namespace SGS.Modules.CAL.Application.Services;

/// <summary>
/// 假日資料的年度快取（Singleton）：工作日運算幾乎每個請求都會讀，改以「年度」為單位快取，假日異動時立即失效；
/// 並設短 TTL（10 分鐘）作為多執行個體部署時其他節點的最終一致保底。
/// </summary>
public sealed class HolidayCache(IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private static string Key(int year) => $"cal:holidays:{year}";

    public bool TryGet(int year, out IReadOnlyList<CalendarDayOverride> items)
    {
        if (cache.TryGetValue(Key(year), out IReadOnlyList<CalendarDayOverride>? cached) && cached is not null)
        {
            items = cached;
            return true;
        }

        items = [];
        return false;
    }

    public void Set(int year, IReadOnlyList<CalendarDayOverride> items) => cache.Set(Key(year), items, Ttl);

    public void Invalidate(IEnumerable<int> years)
    {
        foreach (var year in years.Distinct()) cache.Remove(Key(year));
    }
}

/// <summary>
/// Core <see cref="IWorkCalendarProvider"/> 的實作：PMS 等模組的工作日運算經此取得假日與補班日。
/// 以年度為單位讀取並快取，區間查詢由快取過濾。
/// </summary>
public sealed class WorkCalendarProvider(CALDbContext db, HolidayCache cache) : IWorkCalendarProvider
{
    public async Task<IReadOnlyList<CalendarDayOverride>> GetOverridesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (to < from) return [];

        var result = new List<CalendarDayOverride>();
        for (var year = from.Year; year <= to.Year; year++)
        {
            if (!cache.TryGet(year, out var items))
            {
                var start = new DateOnly(year, 1, 1);
                var end = new DateOnly(year, 12, 31);
                items = await db.Holidays.AsNoTracking()
                    .Where(h => h.HolidayDate >= start && h.HolidayDate <= end)
                    .OrderBy(h => h.HolidayDate)
                    .Select(h => new CalendarDayOverride(h.HolidayDate, h.IsWorkday, h.Name))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                cache.Set(year, items);
            }

            result.AddRange(items.Where(i => i.Date >= from && i.Date <= to));
        }

        return result;
    }
}

/// <summary>假日與補班日維護：查詢、批次新增／更新、刪除與 CSV 匯入（預覽後確認）。異動後使快取失效。</summary>
public sealed class HolidayAdminService(CALDbContext db, HolidayCache cache, TimeProvider clock)
{
    public async Task<IReadOnlyList<HolidayDto>> SearchAsync(int? year, CancellationToken ct)
    {
        var query = db.Holidays.AsNoTracking();
        if (year is { } y)
        {
            var from = new DateOnly(y, 1, 1);
            var to = new DateOnly(y, 12, 31);
            query = query.Where(h => h.HolidayDate >= from && h.HolidayDate <= to);
        }

        return await query.OrderBy(h => h.HolidayDate)
            .Select(h => new HolidayDto(h.HolidayDate, h.Name, h.IsWorkday, h.Source)).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<CalResult<UpsertHolidaysResultDto>> UpsertAsync(string actor, UpsertHolidaysRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invalid = HolidayRules.ValidateBatch(request.Items);
        if (invalid is not null) return CalError.Validation(invalid);

        var count = await SaveAsync(actor, HolidayRules.Dedupe(request.Items), ct).ConfigureAwait(false);
        return CalResult<UpsertHolidaysResultDto>.Success(new UpsertHolidaysResultDto(count));
    }

    public async Task<CalResult<bool>> DeleteAsync(DateOnly date, CancellationToken ct)
    {
        var row = await db.Holidays.FirstOrDefaultAsync(h => h.HolidayDate == date, ct).ConfigureAwait(false);
        if (row is null) return CalError.NotFound("假日不存在。");

        db.Holidays.Remove(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        cache.Invalidate([date.Year]);
        return CalResult<bool>.Success(true);
    }

    /// <summary>
    /// CSV 匯入：先解析並標示每列是新增或覆蓋（Commit=false 只預覽）；有任何格式錯誤時不寫入，須修正後重送；Commit=true 且無錯誤才寫入。
    /// </summary>
    public async Task<CalResult<HolidayImportResultDto>> ImportAsync(string actor, HolidayImportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parsed = HolidayCsv.Parse(request.Csv);
        var dates = parsed.Rows.Select(r => r.Item.Date).ToList();
        var existing = dates.Count == 0
            ? []
            : await db.Holidays.AsNoTracking().Where(h => dates.Contains(h.HolidayDate)).Select(h => h.HolidayDate).ToListAsync(ct).ConfigureAwait(false);
        var existingSet = existing.ToHashSet();

        var rows = parsed.Rows.Select(r => new HolidayImportRowDto(r.Line, r.Item.Date, r.Item.Name, r.Item.IsWorkday, !existingSet.Contains(r.Item.Date))).ToList();
        var newCount = rows.Count(r => r.IsNew);
        var updateCount = rows.Count - newCount;

        var committed = false;
        if (request.Commit && parsed.Errors.Count == 0 && rows.Count > 0)
        {
            await SaveAsync(actor, parsed.Rows.Select(r => r.Item).ToList(), ct).ConfigureAwait(false);
            committed = true;
        }
        else if (request.Commit && parsed.Errors.Count > 0)
        {
            return CalError.Validation("匯入內容仍有格式錯誤，請修正後重新預覽。");
        }

        return CalResult<HolidayImportResultDto>.Success(new HolidayImportResultDto(rows, parsed.Errors, parsed.Warnings, newCount, updateCount, committed));
    }

    private async Task<int> SaveAsync(string actor, IReadOnlyList<HolidayDto> items, CancellationToken ct)
    {
        var dates = items.Select(i => i.Date).ToList();
        var existing = await db.Holidays.Where(h => dates.Contains(h.HolidayDate)).ToDictionaryAsync(h => h.HolidayDate, ct).ConfigureAwait(false);

        var now = clock.GetUtcNow();
        foreach (var item in items)
        {
            if (existing.TryGetValue(item.Date, out var row))
            {
                row.Name = item.Name.Trim();
                row.IsWorkday = item.IsWorkday;
                row.Source = HolidaySource.Manual; // 人工編輯後即為人工維護，之後的排程同步不會覆寫
                row.UpdatedBy = actor;
                row.UpdatedAtUtc = now;
            }
            else
            {
                db.Holidays.Add(new Holiday
                {
                    HolidayDate = item.Date, Name = item.Name.Trim(), IsWorkday = item.IsWorkday, Source = HolidaySource.Manual, CreatedBy = actor, CreatedAtUtc = now
                });
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        cache.Invalidate(items.Select(i => i.Date.Year));
        return items.Count;
    }
}
