using System.Security.Claims;

namespace SGSFramework.ReportEngine.Abstractions
{
    /// <summary>
    /// 報表存取行為類型（ISO 17025 稽核軌跡：檢視、下載、列印、真偽查驗）
    /// </summary>
    public enum ReportAccessAction
    {
        View = 1,
        Download = 2,
        Print = 3,
        Verify = 4
    }

    /// <summary>
    /// 報表存取稽核記錄器；未註冊實作時由 <see cref="NullReportAccessLogger"/> 取代（不記錄）
    /// </summary>
    public interface IReportAccessLogger
    {
        Task LogAsync(
            ClaimsPrincipal user,
            string reportTypeCode,
            string? reportKey,
            ReportAccessAction action,
            CancellationToken cancellationToken = default);
    }

    public sealed class NullReportAccessLogger : IReportAccessLogger
    {
        public static readonly NullReportAccessLogger Instance = new();

        public Task LogAsync(
            ClaimsPrincipal user,
            string reportTypeCode,
            string? reportKey,
            ReportAccessAction action,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
