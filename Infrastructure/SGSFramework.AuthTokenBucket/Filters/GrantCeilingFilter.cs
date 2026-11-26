using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.Abstractions.Permissions.Repositories;
using SGSFramework.Core.Rules;
using SGSFramework.AuthTokenBucket.Abstractions;

namespace SGSFramework.AuthTokenBucket.Filters;

/// <summary>授權上限檢查的類型。</summary>
public enum GrantKind
{
    /// <summary>指派使用者角色（請求含 RoleNames）。</summary>
    Roles,
    /// <summary>指派使用者直接權限（請求含 Permissions，可帶 tenantLabId 查詢參數）。</summary>
    DirectPermissions,
    /// <summary>指派使用者實驗室專屬權限（請求含 ControllerOrModuleKey 與 Bitmask）。</summary>
    LabPermission
}

/// <summary>
/// 授權上限：指派者只能授與自己已擁有的權限。是否啟用由動態規則（RUL）決定：
/// 存在命中的 Column 規則「欄位 GrantBeyondOwn 寫入 ForceDeny」（預設規則「不得授與超過自己的權限」）才檢查，
/// 經審批停用該規則即不檢查。系統管理員不受限。目標以路由參數 <c>userId</c> 指定。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EnforceGrantCeilingAttribute : TypeFilterAttribute
{
    public const string RuleField = "GrantBeyondOwn";

    public EnforceGrantCeilingAttribute(GrantKind kind) : base(typeof(GrantCeilingFilter)) => Arguments = [kind];
}

public sealed class GrantCeilingFilter(IServiceProvider services, GrantKind kind) : IAsyncActionFilter
{
    private const string DefaultMessage = "依動態規則，您不得授與超過自己所擁有的權限。";
    private const string RolePermissionClaim = "Permission";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var ct = context.HttpContext.RequestAborted;
        var excess = await FindExcessAsync(context, ct).ConfigureAwait(false);
        if (excess is { Count: > 0, Message: { } message })
        {
            var shown = excess.Items.Take(8).ToList();
            var more = excess.Items.Count > shown.Count ? $" 等 {excess.Items.Count} 項" : string.Empty;
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"{message}超出的項目：{string.Join("、", shown)}{more}。",
                Instance = context.HttpContext.Request.Path
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next().ConfigureAwait(false);
    }

    private sealed record Excess(string? Message, List<string> Items)
    {
        public int Count => Items.Count;
    }

    private async Task<Excess?> FindExcessAsync(ActionExecutingContext context, CancellationToken ct)
    {
        var engine = services.GetService<IRuleEngine>();
        var current = services.GetRequiredService<ICurrentUserService>();
        if (engine is null || !current.IsAuthenticated || current.IsAdmin) return null;
        if (!context.RouteData.Values.TryGetValue("userId", out var raw) || !Guid.TryParse(raw?.ToString(), out var targetId)) return null;

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var target = await userManager.FindByIdAsync(targetId.ToString()).ConfigureAwait(false);
        if (target is null) return null; // 目標不存在交由端點本身回應

        var ctx = new RuleUserContext { UserId = current.UserId ?? string.Empty, Roles = current.Roles, IsAdmin = current.IsAdmin };
        var denial = engine.FindWriteDenial(ctx, target, EnforceGrantCeilingAttribute.RuleField);
        if (denial is null) return null; // 規則未啟用：不檢查

        var actorKeys = new HashSet<string>(current.Permissions, StringComparer.OrdinalIgnoreCase);
        var items = kind switch
        {
            GrantKind.Roles => await RoleExcessAsync(context, userManager, target, actorKeys).ConfigureAwait(false),
            GrantKind.DirectPermissions => await DirectExcessAsync(context, targetId, actorKeys, ct).ConfigureAwait(false),
            _ => await LabExcessAsync(context, actorKeys, ct).ConfigureAwait(false)
        };

        var message = string.IsNullOrWhiteSpace(denial.Message) ? DefaultMessage : denial.Message;
        return new Excess(message, items);
    }

    /// <summary>新增指派的角色中，含有指派者沒有的權限者。</summary>
    private async Task<List<string>> RoleExcessAsync(ActionExecutingContext context, UserManager<ApplicationUser> userManager, ApplicationUser target, HashSet<string> actorKeys)
    {
        var requested = ReadStrings(context, "RoleNames");
        var currentRoles = new HashSet<string>(await userManager.GetRolesAsync(target).ConfigureAwait(false), StringComparer.OrdinalIgnoreCase);
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        var result = new List<string>();
        foreach (var name in requested.Where(n => !currentRoles.Contains(n)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = await roleManager.FindByNameAsync(name).ConfigureAwait(false);
            if (role is null) continue;
            var claims = await roleManager.GetClaimsAsync(role).ConfigureAwait(false);
            var missing = claims.Where(c => c.Type == RolePermissionClaim && !actorKeys.Contains(c.Value)).Select(c => c.Value).Distinct().Count();
            if (missing > 0) result.Add($"角色「{name}」（含您沒有的 {missing} 項權限）");
        }
        return result;
    }

    /// <summary>新增的直接權限（相對目標目前已有者）中，指派者沒有的權限鍵值。</summary>
    private async Task<List<string>> DirectExcessAsync(ActionExecutingContext context, Guid targetId, HashSet<string> actorKeys, CancellationToken ct)
    {
        var requested = ReadStrings(context, "Permissions").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (requested.Count == 0) return [];

        var bitmask = services.GetRequiredService<IPermissionBitmaskService>();
        var repo = services.GetRequiredService<IUserPermissionRepository>();

        var requestedMasks = await bitmask.CalculateModuleBitmasksAsync(requested, ct).ConfigureAwait(false);
        Guid? tenantLabId = context.ActionArguments.TryGetValue("tenantLabId", out var t) ? t as Guid? : null;
        var existing = tenantLabId is { } lab && lab != Guid.Empty
            ? await repo.GetPermissionsByLabAsync(targetId.ToString(), lab, ct).ConfigureAwait(false)
            : await repo.GetGlobalPermissionsAsync(targetId.ToString(), ct).ConfigureAwait(false);

        var added = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var (prefix, mask) in requestedMasks)
        {
            var had = existing is not null && existing.TryGetValue(prefix, out var e) ? e : 0L;
            var bits = mask & ~had;
            if (bits != 0) added[prefix] = bits;
        }

        return await DecodeBeyondAsync(added, actorKeys, ct).ConfigureAwait(false);
    }

    /// <summary>實驗室專屬權限：保守檢查請求的所有位元（不查目標原有值），超出指派者權限者視為超出。</summary>
    private async Task<List<string>> LabExcessAsync(ActionExecutingContext context, HashSet<string> actorKeys, CancellationToken ct)
    {
        var request = context.ActionArguments.Values.FirstOrDefault(v => v?.GetType().GetProperty("ControllerOrModuleKey") is not null);
        if (request is null) return [];

        var key = request.GetType().GetProperty("ControllerOrModuleKey")!.GetValue(request) as string;
        var mask = request.GetType().GetProperty("Bitmask")?.GetValue(request) is long m ? m : 0L;
        if (string.IsNullOrWhiteSpace(key) || mask == 0) return [];

        return await DecodeBeyondAsync(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { [key] = mask }, actorKeys, ct).ConfigureAwait(false);
    }

    private async Task<List<string>> DecodeBeyondAsync(Dictionary<string, long> masks, HashSet<string> actorKeys, CancellationToken ct)
    {
        if (masks.Count == 0) return [];
        var bitmask = services.GetRequiredService<IPermissionBitmaskService>();
        var keys = await bitmask.DecodeBitmaskToPermissionsAsync(masks, ct).ConfigureAwait(false);
        return keys.Where(k => !actorKeys.Contains(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>從動作參數中找出帶有指定字串清單屬性的請求物件並讀取。</summary>
    private static List<string> ReadStrings(ActionExecutingContext context, string propertyName)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            var prop = arg?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(arg) is IEnumerable<string> list) return list.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }
        return [];
    }
}
