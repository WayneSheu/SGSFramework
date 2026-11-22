using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.MTG.Application.Contracts;

namespace SGS.Modules.MTG.Controllers.V1;

/// <summary>MTG 控制器共用：目前使用者、MtgResult → ProblemDetails 映射（含 MTG 業務錯誤碼 code）。</summary>
public abstract class MtgControllerBase : ControllerBase
{
    protected string Actor =>
        User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "未知使用者";

    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    /// <summary>PMS 專案可見範圍：具 PMS.PROJECT.ADMIN（檢視所有專案）者 ViewAll，其餘只看自己是成員的專案。</summary>
    protected async Task<ProjectScope> ProjectScopeAsync(CancellationToken ct)
    {
        var viewAll = await HttpContext.RequestServices.GetRequiredService<SGSFramework.AuthTokenBucket.Abstractions.IPermissionAuthorizationService>()
            .HasPermissionAsync(User, "PMS.PROJECT.ADMIN", ct).ConfigureAwait(false);
        return new ProjectScope(CurrentUserId, viewAll);
    }

    protected ActionResult<T> FromResult<T>(MtgResult<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error!);

    protected ActionResult NoContentFrom(MtgResult<bool> result) =>
        result.IsSuccess ? NoContent() : ProblemFrom(result.Error!);

    protected ObjectResult ProblemFrom(MtgError error)
    {
        var problem = new ProblemDetails
        {
            Status = error.Status,
            Title = error.Title,
            Detail = error.Detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        problem.Extensions["timestamp"] = DateTime.UtcNow;
        return StatusCode(error.Status, problem);
    }
}
