using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.RPT.Controllers.V1;

/// <summary>
/// 報告類別管理：類別主檔（檢驗領域）、適用範圍（全域 / 實驗室專屬）、承作實驗室、儲存位置與資安管控。
/// 授權與簽署人資格見 <see cref="ReportAuthorizationController"/>。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/report-categories")]
[ControllerTitle("報告類別管理", Icon = "fa-solid fa-file-shield", Order = 3, Description = "報告類別（檢驗領域）、適用範圍、承作實驗室、儲存位置與資安管控")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ReportCategoryController(IReportCategoryManagementService managementService) : ControllerBase
{
    private readonly IReportCategoryManagementService _service = managementService ?? throw new ArgumentNullException(nameof(managementService));

    
    public sealed record ReviewRequest(string? Comment);

    // ===== 類別主檔 =====

    /// <summary>查詢報告類別（含承作實驗室）</summary>
    [HttpGet]
    [Function("GetReportCategories", "報告類別管理", Icon = "fa-solid fa-file-shield", Order = 1, IsMenu = true, Path = "/sgs.modules.rpt/reportcategory", Description = "查詢報告類別主檔、適用範圍、承作實驗室、儲存位置與資安管控")]
    [RequiresPermission("RPT.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default) =>
        Ok(await _service.ListCategoriesAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>報告類別分頁查詢（篩選、排序、定位指定類別所在頁）</summary>
    [HttpPost("search")]
    [RequiresPermission("RPT.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(ReportCategorySearchResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] ReportCategorySearchRequest request, CancellationToken cancellationToken = default) =>
        Ok(await _service.SearchCategoriesAsync(request ?? new ReportCategorySearchRequest(), cancellationToken).ConfigureAwait(false));

    /// <summary>新增或更新報告類別</summary>
    [HttpPost]
    [Function("SaveReportCategory", "維護報告類別", Icon = "fa-solid fa-pen", Order = 2, Description = "新增或更新報告類別主檔")]
    [RequiresPermission("RPT.REPORTCATEGORY.MANAGE", "維護報告類別")]
    [ProducesResponseType(typeof(ReportCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Save([FromBody] SaveReportCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (ok, message, category) = await _service.SaveCategoryAsync(request, labScoped: false, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(category) : Problem400("儲存報告類別失敗", message);
    }

    /// <summary>設定類別的承作實驗室（整批取代）</summary>
    [HttpPut("{code}/labs")]
    [Function("SetReportCategoryLabs", "設定承作實驗室", Icon = "fa-solid fa-building", Order = 3, Description = "設定可承作此報告類別的區域實驗室")]
    [RequiresPermission("RPT.REPORTCATEGORY.MANAGE", "維護報告類別")]
    public async Task<IActionResult> SetLabs([FromRoute] string code, [FromBody] SetCategoryLabsRequest request, CancellationToken cancellationToken = default)
    {
        var (ok, message) = await _service.SetCategoryLabsAsync(code, request?.TenantLabIds ?? [], cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new { Succeeded = true, Message = message }) : Problem400("設定承作實驗室失敗", message);
    }

    /// <summary>新增或更新目前實驗室的實驗室專屬類別（實驗室層級維護者）</summary>
    [HttpPost("lab-scoped")]
    [Function("SaveLabReportCategory", "維護實驗室專屬類別", Icon = "fa-solid fa-flask", Order = 3, Description = "實驗室管理者維護目前實驗室的實驗室專屬報告類別")]
    [RequiresPermission(ReportCategoryManagementService.ManageLabPermission, "維護實驗室專屬報告類別")]
    [ProducesResponseType(typeof(ReportCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveLabScoped([FromBody] SaveReportCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (ok, message, category) = await _service.SaveCategoryAsync(request, labScoped: true, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(category) : Problem400("儲存報告類別失敗", message);
    }

    /// <summary>目前使用者的報告類別維護範圍</summary>
    [HttpGet("manage-scope")]
    [Function("GetReportCategoryManageScope", "查詢類別維護範圍", Icon = "fa-solid fa-user-gear", Order = 10, Description = "查詢目前使用者可維護的報告類別範圍")]
    [RequiresPermission("RPT.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(ReportCategoryManageScopeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ManageScope(CancellationToken cancellationToken = default) =>
        Ok(await _service.GetManageScopeDtoAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>可選用的儲存分類（檔案管理設定）</summary>
    [HttpGet("storage-categories")]
    [Function("GetReportStorageCategories", "查詢儲存分類", Icon = "fa-solid fa-hard-drive", Order = 11, Description = "查詢報告類別可選用的儲存分類（檔案管理設定）")]
    [RequiresPermission("RPT.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(IReadOnlyList<FileStorageCategoryInfo>), StatusCodes.Status200OK)]
    public IActionResult StorageCategories() => Ok(_service.GetStorageCategories());

    /// <summary>區域實驗室選項</summary>
    [HttpGet("lab-options")]
    [Function("GetReportLabOptions", "查詢區域實驗室選項", Icon = "fa-solid fa-building", Order = 12, Description = "查詢報告類別可指定的區域實驗室選項")]
    [RequiresPermission("RPT.REPORTCATEGORY.READ", "檢視報告類別權限")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportLabOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LabOptions(CancellationToken cancellationToken = default) =>
        Ok(await _service.GetLabOptionsAsync(cancellationToken).ConfigureAwait(false));


    private BadRequestObjectResult Problem400(string title, string detail) =>
        BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = title, Detail = detail, Instance = HttpContext.Request.Path });
}
