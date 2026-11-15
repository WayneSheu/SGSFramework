using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGS.Modules.PMS.Application.Services;

namespace SGS.Modules.PMS.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>註冊 PMS Application 層用例（M1：專案、成員、Gate、交付物）。</summary>
    public static IServiceCollection AddModulePmsApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ProjectService>();
        // IFileManagementService 由 Core 預設（NullFileManagementService，503）或 FMS 外掛提供
        services.AddScoped<DeliverableFileService>();
        services.AddScoped<HolidayService>();
        services.AddScoped<TicketService>();
        services.AddScoped<TicketAttachmentService>();
        services.AddScoped<ChangeRequestService>();
        services.AddScoped<ChangeRequestWatchService>();
        services.AddScoped<ReleaseChecklistService>();
        services.AddScoped<RcaService>();
        services.AddScoped<UatService>();
        services.AddScoped<RiskService>();
        services.AddScoped<GanttService>();
        services.AddScoped<GanttExportService>();
        services.AddScoped<ContractService>();
        services.AddScoped<ContractAttachmentService>();
        services.AddScoped<ContractFulfillmentService>();
        services.AddScoped<ContractWatchService>();
        services.AddScoped<UatAttachmentService>();
        services.AddScoped<PmsMailNotifier>();
        services.AddScoped<MonthlyReportService>();
        return services;
    }
}
