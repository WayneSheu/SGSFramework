namespace SGSFramework.Core.Calendars;

/// <summary>
/// 工作日例外：某一天的實際上班狀態與別名。<paramref name="IsWorkday"/> 為 false 表示放假（國定假日、公司休假），
/// 為 true 表示補班（週末上班）。未列出的日期依週一至週五為工作日、週六日為休息日。
/// </summary>
public readonly record struct CalendarDayOverride(DateOnly Date, bool IsWorkday, string Name);

/// <summary>
/// 公司工作日曆提供者（Core 契約）：由 SGS.Modules.CAL 外掛實作，其他模組（PMS 的 SLA、合約工作日、加班判定等）只依賴此介面，
/// 不直接參照 CAL。CAL 未安裝時由 <see cref="NullWorkCalendarProvider"/> 接管，行為等同純週一至週五。
/// </summary>
public interface IWorkCalendarProvider
{
    /// <summary>取得 [from, to]（含）區間內的假日與補班日，依日期排序；區間內沒有例外時回傳空集合。</summary>
    Task<IReadOnlyList<CalendarDayOverride>> GetOverridesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <summary>未載入 SGS.Modules.CAL 時的預設實作：沒有任何假日例外（純週一至週五工作日）。</summary>
public sealed class NullWorkCalendarProvider : IWorkCalendarProvider
{
    public Task<IReadOnlyList<CalendarDayOverride>> GetOverridesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CalendarDayOverride>>([]);
}
