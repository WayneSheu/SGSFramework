using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SGSFramework.Core.Abstractions.AuditLogs;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.DateTimes;
using SGSFramework.Core.DateTimes.Providers;
using SGSFramework.Core.HttpAuditProviders;

namespace SGSFramework.AuditLog.Extensions;

public static class DateTimesExtensions
{
    public static IServiceCollection AddTaiwanDateTimeProvider(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, TaiwanDateTimeProvider>();

        return services;
    }
}