using System.Net;
using Hangfire.Dashboard;
using SGSFramework.Core.Abstractions.Identities;

namespace SGSFramework.Scheduler.Hosting;

/// <summary>
/// Hangfire Dashboard 存取控制：預設僅本機；關閉 LocalRequestsOnly 時須為已驗證的系統管理員 (IsSuperAdmin)。
/// 系統帳號權杖一律拒絕（M2M 帳號不得操作管理介面）。
/// </summary>
internal sealed class SchedulerDashboardAuthorizationFilter(bool localRequestsOnly) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var connection = httpContext.Connection;

        if (localRequestsOnly)
        {
            return connection.RemoteIpAddress is { } remote &&
                   (IPAddress.IsLoopback(remote) || remote.Equals(connection.LocalIpAddress));
        }

        var user = httpContext.User;
        if (user.Identity?.IsAuthenticated != true) return false;
        if (user.IsSystemAccount()) return false;

        return user.HasClaim(c =>
            (c.Type.Equals("IsSuperAdmin", StringComparison.OrdinalIgnoreCase) || c.Type == "is_admin") &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
    }
}
