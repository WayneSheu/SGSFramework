using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 角色異動歷程：寫入稽核紀錄（操作者自動取自目前請求）並提供查詢。
/// 寫入失敗僅記錄警告，不影響已完成的業務異動。
/// </summary>
public interface IRoleAuditService
{
    Task RecordAsync(Guid roleId, string roleName, string action, string summary,
        int affectedUserCount = 0, object? detail = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleAuditLog>> GetHistoryAsync(Guid roleId, int take = 100, CancellationToken cancellationToken = default);
}

public sealed class RoleAuditService(
    DbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<RoleAuditService> logger) : IRoleAuditService
{
    private static readonly JsonSerializerOptions DetailJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task RecordAsync(Guid roleId, string roleName, string action, string summary,
        int affectedUserCount = 0, object? detail = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = httpContextAccessor.HttpContext?.User;
            var actorIdRaw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;

            dbContext.Set<RoleAuditLog>().Add(new RoleAuditLog
            {
                RoleId = roleId,
                RoleName = roleName,
                Action = action,
                Summary = summary.Length > 1000 ? summary[..1000] : summary,
                AffectedUserCount = affectedUserCount,
                Detail = detail is null ? null : JsonSerializer.Serialize(detail, DetailJsonOptions),
                ActorUserId = Guid.TryParse(actorIdRaw, out var actorId) ? actorId : null,
                // 背景排程（如過渡期角色到期回收）無使用者上下文
                ActorName = user?.Identity?.Name ?? user?.FindFirst(ClaimTypes.Name)?.Value ?? "系統排程",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[RoleAudit] 寫入角色異動歷程失敗。RoleId: {RoleId}, Action: {Action}", roleId, action);
        }
    }

    public async Task<IReadOnlyList<RoleAuditLog>> GetHistoryAsync(Guid roleId, int take = 100, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<RoleAuditLog>()
            .AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
