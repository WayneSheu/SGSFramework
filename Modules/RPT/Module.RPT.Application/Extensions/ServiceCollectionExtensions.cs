using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGS.Modules.RPT.Application.Services;
using SGSFramework.ReportEngine.Abstractions;

namespace SGS.Modules.RPT.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Application 層：報告生命週期服務、授權門面、存取稽核與流程通知
    /// </summary>
    public static IServiceCollection AddModuleRptApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IReportAccessGuard, ReportAccessGuard>();
        services.AddScoped<ReportAccessLogger>();
        // 框架報表下載（ReportEngine 基底控制器）亦寫入同一份不可竄改的存取紀錄
        services.AddScoped<IReportAccessLogger>(sp => sp.GetRequiredService<ReportAccessLogger>());
        services.AddScoped<IReportWorkflowNotifier, ReportWorkflowNotifier>();
        services.AddScoped<IReportService, ReportService>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
