using SGS.Modules.PMS.Application.Rules;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Contracts;

/// <summary>月度儀表板（即時重算）。</summary>
public sealed record MonthlyDashboardDto(
    Guid ProjectId, string ProjectCode, string ProjectName, string Period, bool IsCurrentMonth, DateTimeOffset AsOf,
    int TotalIncoming, int SlaApplicableCount, int SlaMetOnTimeCount, decimal? SlaOnTimeRate, decimal? MttrHours,
    IReadOnlyList<TypeCount> TypeRatio, int OverdueCount, int DueSoonCount, bool CanGenerate);

public sealed record MonthlyReportSearchFilter
{
    public Guid? ProjectId { get; init; }

    /// <summary>起訖月份（含，YYYY-MM）。</summary>
    public string? PeriodFrom { get; init; }

    public string? PeriodTo { get; init; }
}

public sealed record MonthlyReportSearchRequest : PagedQueryParameters<MonthlyReportSearchFilter>;

public sealed record MonthlyReportDto(
    Guid Id, Guid ProjectId, string ProjectCode, string ProjectName, string Period, int Revision, string FileName, long FileSize,
    int TotalIncoming, decimal? SlaOnTimeRate, decimal? MttrHours, int OverdueCount, int DueSoonCount, Guid GeneratedBy, DateTimeOffset GeneratedAt);

public sealed record GenerateMonthlyReportRequest(Guid ProjectId, string Period);

/// <summary>月報自動產生結果（排程以此顯示作業摘要）。</summary>
/// <param name="Periods">本次處理的月份（空＝今天不是產生日）。</param>
public sealed record MonthlyAutoGenerateResult(
    IReadOnlyList<string> Periods, int Eligible, int Generated, int SkippedExisting, int Failed, int Notified, string Message);
