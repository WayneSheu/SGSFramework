using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// 掃描已載入組件中的 DbContext 型別，並自 DI 解析已註冊者 (PhysLIMSDbContext、ORGDbContext…)
/// </summary>
internal static class DbContextDiscovery
{
    private static readonly Lazy<Type[]> ContextTypes = new(DiscoverTypes);

    /// <remarks>
    /// Host 會把 PhysLIMSDbContext 同時註冊為基底 DbContext，同一實體可能由多個型別解析而來，
    /// 因此以實體參照去重，避免同一 DbContext 被處理兩次 (例如更新履歷重複)。
    /// </remarks>
    public static IEnumerable<DbContext> ResolveRegistered(IServiceProvider serviceProvider, ILogger logger)
    {
        var seen = new HashSet<DbContext>(ReferenceEqualityComparer.Instance);
        foreach (var contextType in ContextTypes.Value)
        {
            DbContext? context;
            try
            {
                context = serviceProvider.GetService(contextType) as DbContext;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "略過無法解析的 DbContext: {ContextType}", contextType.FullName);
                continue;
            }

            if (context is not null && seen.Add(context))
            {
                yield return context;
            }
        }
    }

    private static Type[] DiscoverTypes()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.OfType<Type>();
                }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && t != typeof(DbContext) && typeof(DbContext).IsAssignableFrom(t))
            .ToArray();
    }
}
