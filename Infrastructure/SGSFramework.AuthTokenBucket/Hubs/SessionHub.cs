using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SGSFramework.AuthTokenBucket.Hubs;

/// <summary>
/// 工作階段即時通知 Hub（Blazor 以 .NET SignalR Client、Vue 3 以 @microsoft/signalr 連線）。
/// 連線時依 JWT 的使用者與 device_id 加入群組，工作階段被撤銷時由伺服器主動推播
/// <see cref="SessionHubEvents.SessionRevoked"/>，讓其他裝置即時登出。
/// </summary>
/// <remarks>
/// 連線同樣經過 JwtBearer 與 OnTokenValidated 工作階段檢查，已撤銷的裝置無法重新連線。
/// 瀏覽器 WebSocket 無法帶 Authorization 標頭，Token 以 access_token 查詢參數傳遞（見 JwtBearer OnMessageReceived）。
/// </remarks>
[Authorize]
public sealed class SessionHub : Hub
{
    public const string Route = "/hubs/session";

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId(Context.User);
        var deviceId = Context.User?.FindFirst("device_id")?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionHubGroups.User(userId));
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, SessionHubGroups.Device(userId, deviceId));
        }

        await base.OnConnectedAsync();
    }

    internal static string? GetUserId(ClaimsPrincipal? user) =>
        user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user?.FindFirst("sub")?.Value
        ?? user?.FindFirst("nameid")?.Value;
}

public static class SessionHubGroups
{
    public static string User(string userId) => $"user:{userId}";
    public static string Device(string userId, string deviceId) => $"device:{userId}:{deviceId.ToUpperInvariant()}";
}

/// <summary>前端訂閱的事件名稱（Blazor 與 Vue 共用）</summary>
public static class SessionHubEvents
{
    public const string SessionRevoked = "SessionRevoked";
}

/// <summary>SessionRevoked 推播內容</summary>
/// <param name="Reason">logout-all / logout / revoked</param>
/// <param name="RevokedAt">撤銷時間 (UTC)</param>
public sealed record SessionRevokedMessage(string Reason, DateTimeOffset RevokedAt);
