using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>國定假日／補班日維護，並提供 SLA 計算使用的工作日曆。</summary>
public sealed class HolidayService(PMSDbContext db, TimeProvider clock)
{
    private const int MaxBatch = 400;

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
            .Select(h => new HolidayDto(h.HolidayDate, h.Name, h.IsWorkday)).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<UpsertHolidaysResultDto>> UpsertAsync(string actor, UpsertHolidaysRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Items is null || request.Items.Count == 0) return PmsError.Validation("請至少提供一筆假日。");
        if (request.Items.Count > MaxBatch) return PmsError.Validation($"單次最多 {MaxBatch} 筆。");

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 50)
                return PmsError.Validation($"{item.Date:yyyy-MM-dd} 的名稱為必填且不得超過 50 字。");
            if (item.Date.Year is < 2000 or > 2100) return PmsError.Validation($"日期 {item.Date:yyyy-MM-dd} 不在合理範圍。");
        }

        // 同批次重複日期以最後一筆為準
        var items = request.Items.GroupBy(i => i.Date).Select(g => g.Last()).ToList();
        var dates = items.Select(i => i.Date).ToList();
        var existing = await db.Holidays.Where(h => dates.Contains(h.HolidayDate)).ToDictionaryAsync(h => h.HolidayDate, ct).ConfigureAwait(false);

        var now = clock.GetUtcNow();
        foreach (var item in items)
        {
            if (existing.TryGetValue(item.Date, out var row))
            {
                row.Name = item.Name.Trim();
                row.IsWorkday = item.IsWorkday;
                row.UpdatedBy = actor;
                row.UpdatedAtUtc = now;
            }
            else
            {
                db.Holidays.Add(new Holiday
                {
                    HolidayDate = item.Date, Name = item.Name.Trim(), IsWorkday = item.IsWorkday, CreatedBy = actor, CreatedAtUtc = now
                });
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return PmsResult<UpsertHolidaysResultDto>.Success(new UpsertHolidaysResultDto(items.Count));
    }

    public async Task<PmsResult<bool>> DeleteAsync(DateOnly date, CancellationToken ct)
    {
        var row = await db.Holidays.FirstOrDefaultAsync(h => h.HolidayDate == date, ct).ConfigureAwait(false);
        if (row is null) return PmsError.NotFound("假日不存在。");

        db.Holidays.Remove(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return PmsResult<bool>.Success(true);
    }

    /// <summary>載入指定日期區間（含）的工作日曆，供月度計算（如「月底最後工作日」）使用。</summary>
    public async Task<WorkCalendar> LoadCalendarAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.Holidays.AsNoTracking()
            .Where(h => h.HolidayDate >= from && h.HolidayDate <= to)
            .Select(h => new HolidayEntry(h.HolidayDate, h.IsWorkday)).ToListAsync(ct).ConfigureAwait(false);
        return new WorkCalendar(rows);
    }

    /// <summary>載入起算日前後一段期間的工作日曆（涵蓋最長 2 個工作天加上連假的合理範圍）。</summary>
    public async Task<WorkCalendar> LoadCalendarAsync(DateTimeOffset around, CancellationToken ct)
    {
        var center = DateOnly.FromDateTime(around.ToOffset(SlaCalculator.Offset).DateTime);
        var from = center.AddDays(-7);
        var to = center.AddDays(45);

        var rows = await db.Holidays.AsNoTracking()
            .Where(h => h.HolidayDate >= from && h.HolidayDate <= to)
            .Select(h => new HolidayEntry(h.HolidayDate, h.IsWorkday)).ToListAsync(ct).ConfigureAwait(false);
        return new WorkCalendar(rows);
    }
}
