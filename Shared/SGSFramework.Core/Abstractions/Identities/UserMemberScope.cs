using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.Core.Abstractions.Identities;

/// <summary>
/// 使用者維護範圍（Core 契約）：由動態規則引擎（RUL）的 Row 規則決定目前登入者能看到／維護哪些使用者。
/// 沒有適用其角色的 Row 規則、為系統管理員或引擎未安裝時不受限。
/// </summary>
public interface IUserMemberScope
{
    /// <summary>依規則限縮使用者查詢；不受限時原樣回傳。</summary>
    Task<IQueryable<T>> ApplyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) where T : ApplicationUser;

    /// <summary>目標使用者是否在目前登入者可維護的範圍內（不受限時一律為 true）。</summary>
    Task<bool> CanAccessUserAsync(Guid targetUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 目前登入者對目標使用者的指定欄位（例：Roles、DirectPermissions）寫入是否被規則明確禁止（例如不得指派自己）。
    /// 沒有命中的 ForceDeny 規則或引擎未安裝時回傳 null；命中時回傳訊息（規則酬載 errorMessage，未設定時為空字串）。
    /// </summary>
    Task<string?> GetWriteDenialAsync(Guid targetUserId, string fieldName, CancellationToken cancellationToken = default);
}

/// <summary>
/// 標記會修改目標使用者「角色」或「直接權限」的動作：由 RUL 的 Column 規則（ForceDeny）決定是否禁止（預設：不得指派自己），
/// 命中時回 403。目標以路由參數 <c>userId</c> 或 <c>id</c> 指定。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class DenyRuleWriteAttribute : TypeFilterAttribute
{
    public DenyRuleWriteAttribute(string fieldName) : base(typeof(RuleWriteFilter))
    {
        FieldName = fieldName;
        Arguments = [fieldName]; // 以建構子參數傳入過濾器
    }

    public string FieldName { get; }
}

public sealed class RuleWriteFilter(IServiceProvider services, string fieldName) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var scope = services.GetService<IUserMemberScope>();
        string? denial = null;
        if (scope is not null && MemberScopeFilter.TryGetTargetUser(context, out var target))
            denial = await scope.GetWriteDenialAsync(target, fieldName, context.HttpContext.RequestAborted).ConfigureAwait(false);

        if (denial is not null)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                // 訊息可由規則酬載 errorMessage 設定；未設定時使用預設訊息
                Detail = string.IsNullOrWhiteSpace(denial) ? "依動態規則，您不得變更此使用者的角色或直接權限（例如不可指派給自己）。" : denial,
                Instance = context.HttpContext.Request.Path
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next().ConfigureAwait(false);
    }
}

/// <summary>
/// 標記以路由參數 <c>userId</c> 或 <c>id</c>（Guid）指定目標使用者的動作：目標不在可維護範圍時回 404，避免洩漏資源是否存在。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequiresMemberScopeAttribute() : TypeFilterAttribute(typeof(MemberScopeFilter));

public sealed class MemberScopeFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var scope = services.GetService<IUserMemberScope>();
        if (scope is not null && TryGetTarget(context, out var target) &&
            !await scope.CanAccessUserAsync(target, context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource Not Found",
                Detail = "使用者不存在或不在您可維護的成員範圍內。",
                Instance = context.HttpContext.Request.Path
            })
            { StatusCode = StatusCodes.Status404NotFound };
            return;
        }

        await next().ConfigureAwait(false);
    }

    internal static bool TryGetTargetUser(ActionExecutingContext context, out Guid target) => TryGetTarget(context, out target);

    private static bool TryGetTarget(ActionExecutingContext context, out Guid target)
    {
        foreach (var key in new[] { "userId", "id" })
        {
            if (context.RouteData.Values.TryGetValue(key, out var v) && Guid.TryParse(v?.ToString(), out target)) return true;
        }
        target = Guid.Empty;
        return false;
    }
}
