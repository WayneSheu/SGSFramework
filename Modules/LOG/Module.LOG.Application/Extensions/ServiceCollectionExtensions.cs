using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.LOG.Application.Stores;
using SGSFramework.Core.Logging;

namespace SGS.Modules.LOG.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 以 AddSingleton 覆蓋 Core 的 Null 緩衝 store 與 Host 過渡期的 SQL 實作（最後註冊者勝）。
    /// store 皆經 IDbContextFactory 取得短命 DbContext，可安全為 Singleton。
    /// </summary>
    public static IServiceCollection AddModuleLogApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISystemLogStore, SystemLogStore>();
        services.AddSingleton<ISecurityLogStore, SecurityLogStore>();
        services.AddSingleton<IAuditLogStore, AuditLogStore>();
        services.AddScoped<ILogReadSource, LogReadSource>();
        return services;
    }
}
