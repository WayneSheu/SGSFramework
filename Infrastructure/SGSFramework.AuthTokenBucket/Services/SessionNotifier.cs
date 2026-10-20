using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Hubs;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 工作階段撤銷即時通知。推播失敗不影響撤銷結果（前端 60 秒心跳仍會補位）。
/// </summary>
public interface ISessionNotifier
{
    Task NotifyUserRevokedAsync(string userId, string reason, CancellationToken cancellationToken = default);
    Task NotifyDeviceRevokedAsync(string userId, string deviceId, string reason, CancellationToken cancellationToken = default);
}

public sealed class SessionNotifier : ISessionNotifier
{
    private readonly IHubContext<SessionHub> _hubContext;
    private readonly ILogger<SessionNotifier> _logger;

    public SessionNotifier(IHubContext<SessionHub> hubContext, ILogger<SessionNotifier> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task NotifyUserRevokedAsync(string userId, string reason, CancellationToken cancellationToken = default) =>
        SendAsync(SessionHubGroups.User(userId), reason, cancellationToken);

    public Task NotifyDeviceRevokedAsync(string userId, string deviceId, string reason, CancellationToken cancellationToken = default) =>
        SendAsync(SessionHubGroups.Device(userId, deviceId), reason, cancellationToken);

    private async Task SendAsync(string group, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients.Group(group).SendAsync(
                SessionHubEvents.SessionRevoked,
                new SessionRevokedMessage(reason, DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[SessionNotifier] 推播工作階段撤銷通知失敗，Group: {Group}", group);
        }
    }
}
