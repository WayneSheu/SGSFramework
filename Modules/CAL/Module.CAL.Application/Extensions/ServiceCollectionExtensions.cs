using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGS.Modules.CAL.Application.Services;
using SGSFramework.Core.Calendars;

namespace SGS.Modules.CAL.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>註冊 CAL Application 層：假日維護、日曆活動與 Core 工作日曆提供者（覆寫 Core 的 Null 預設）。</summary>
    public static IServiceCollection AddModuleCalApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<HolidayCache>();
        services.AddScoped<HolidayAdminService>();
        services.AddHttpClient(GovCalendarSyncService.HttpClientName);
        services.AddScoped<GovCalendarSyncService>();
        services.AddScoped<CalendarService>();
        services.AddScoped<CalMailNotifier>();
        services.AddScoped<EventReminderService>();

        // PMS 等模組只依賴 Core 的 IWorkCalendarProvider；CAL 載入時以此實作覆蓋 Core 預設的 NullWorkCalendarProvider
        services.AddScoped<IWorkCalendarProvider, WorkCalendarProvider>();
        return services;
    }
}
