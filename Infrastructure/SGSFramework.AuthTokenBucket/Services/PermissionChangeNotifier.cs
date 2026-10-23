using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Hubs;
using SGSFramework.Core.Abstractions.Entities.Identities;
using ZiggyCreatures.Caching.Fusion;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 權限異動通知：清除受影響使用者的權限快取，並透過 SignalR 即時通知前端重新載入選單與權限。
/// 任何會改變使用者有效權限的操作（角色狀態、角色權限、角色成員、使用者直接權限）都應呼叫。
/// </summary>
public interface IPermissionChangeNotifier
{
    Task NotifyUsersAsync(IEnumerable<string> userIds, string reason, CancellationToken cancellationToken = default);

    /// <summary>通知某角色的所有成員（角色狀態或角色權限異動時使用）</summary>
    Task NotifyRoleMembersAsync(string roleName, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// 廣播給所有已連線使用者（影響全體的異動，例如系統功能啟用 / 停用），前端收到後重新載入選單與系統公告。
    /// 不清除個別使用者的權限快取（有效權限未改變）。
    /// </summary>
    Task BroadcastAsync(string reason, CancellationToken cancellationToken = default);
}

/// <summary>權限快取標籤，供 UserRuntimeScopeService 建立快取時標記、異動時批次清除</summary>
public static class PermissionCacheTags
{
    public static string User(string userId) => $"perm-user:{userId.ToLowerInvariant()}";
}

public sealed class PermissionChangeNotifier(
    IFusionCache cache,
    IPermissionResolver permissionResolver,
    UserManager<ApplicationUser> userManager,
    IHubContext<SessionHub> hubContext,
    ILogger<PermissionChangeNotifier> logger) : IPermissionChangeNotifier
{
    public const string PermissionsChangedEvent = "PermissionsChanged";

    public async Task NotifyUsersAsync(IEnumerable<string> userIds, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0) return;

        foreach (var userId in ids)
        {
            try
            {
                // 1. 清除執行期權限快取（全域與各實驗室範圍）與登入權限遮罩快取，下一次請求即重新計算
                await cache.RemoveByTagAsync(PermissionCacheTags.User(userId), token: cancellationToken).ConfigureAwait(false);
                await permissionResolver.EvictUserCacheAsync(userId).ConfigureAwait(false);

                // 2. 即時通知該使用者所有已連線裝置
                await hubContext.Clients.Group(SessionHubGroups.User(userId)).SendAsync(
                    PermissionsChangedEvent,
                    new { Reason = reason, ChangedAt = DateTimeOffset.UtcNow },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 通知失敗不影響已完成的資料異動；快取最晚於到期後（15 分鐘）自然更新
                logger.LogWarning(ex, "[PermissionChange] 通知使用者 {UserId} 權限異動失敗。", userId);
            }
        }

        logger.LogInformation("[PermissionChange] 已通知 {Count} 位使用者權限異動。原因: {Reason}", ids.Count, reason);
    }

    public async Task NotifyRoleMembersAsync(string roleName, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return;

        var members = await userManager.GetUsersInRoleAsync(roleName).ConfigureAwait(false);
        await NotifyUsersAsync(members.Select(m => m.Id.ToString()), reason, cancellationToken).ConfigureAwait(false);
    }

    public async Task BroadcastAsync(string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            await hubContext.Clients.All.SendAsync(
                PermissionsChangedEvent,
                new { Reason = reason, ChangedAt = DateTimeOffset.UtcNow },
                cancellationToken).ConfigureAwait(false);

            logger.LogInformation("[PermissionChange] 已廣播異動通知給所有連線使用者。原因: {Reason}", reason);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 廣播失敗不影響已完成的資料異動；使用者重新整理後即取得最新選單
            logger.LogWarning(ex, "[PermissionChange] 廣播異動通知失敗。原因: {Reason}", reason);
        }
    }
}
