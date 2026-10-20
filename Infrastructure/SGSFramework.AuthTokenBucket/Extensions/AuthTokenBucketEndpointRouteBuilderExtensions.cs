using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SGSFramework.AuthTokenBucket.Hubs;

namespace SGSFramework.AuthTokenBucket.Extensions;

public static class AuthTokenBucketEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 對應工作階段即時通知 Hub（須置於 UseAuthentication / UseAuthorization 之後）
    /// </summary>
    public static IEndpointRouteBuilder MapTokenBucketHubs(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapHub<SessionHub>(SessionHub.Route);
        return endpoints;
    }
}
