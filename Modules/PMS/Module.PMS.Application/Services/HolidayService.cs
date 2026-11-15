using SGS.Modules.PMS.Application.Rules;
using SGSFramework.Core.Calendars;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 提供 SLA、合約、加班、月報等計算使用的工作日曆。假日與補班日的唯一來源是行事曆管理模組（SGS.Modules.CAL），
/// 此處經 Core 的 <see cref="IWorkCalendarProvider"/> 讀取（CAL 內有年度快取）；CAL 未安裝時沒有任何假日例外（純週一至週五）。
/// 假日維護請至「行事曆管理 &gt; 假日與補班」。
/// </summary>
public sealed class HolidayService(IWorkCalendarProvider provider)
{
    /// <summary>載入指定日期區間（含）的工作日曆，供月度計算（如「月底最後工作日」）使用。</summary>
    public async Task<WorkCalendar> LoadCalendarAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var overrides = await provider.GetOverridesAsync(from, to, ct).ConfigureAwait(false);
        return new WorkCalendar(overrides.Select(o => new HolidayEntry(o.Date, o.IsWorkday)));
    }

    /// <summary>載入起算日前後一段期間的工作日曆（涵蓋最長 2 個工作天加上連假的合理範圍）。</summary>
    public Task<WorkCalendar> LoadCalendarAsync(DateTimeOffset around, CancellationToken ct)
    {
        var center = DateOnly.FromDateTime(around.ToOffset(SlaCalculator.Offset).DateTime);
        return LoadCalendarAsync(center.AddDays(-7), center.AddDays(45), ct);
    }
}
