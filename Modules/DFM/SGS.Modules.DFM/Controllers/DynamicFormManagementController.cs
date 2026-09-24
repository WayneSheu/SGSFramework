using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using System.Net.Mime;

namespace SGS.Modules.DFM.Controllers;

/// <summary>
/// 動態表單管理控制器
/// </summary>
[ApiController]
[ApiVersion("v1")]
[Route("api/v1/forms")]
[ControllerTitle("動態表單管理", Icon = "fa-solid fa-flask", Order = 10, Description = "提供ME實驗室相關報表數據準備、分類查詢與 PDF 下載服務。")]
[RequiresPermission("ORG.MELABORATORYREPORT.READ")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public class DynamicFormManagementController : ApiControllerBase
{
    private readonly ILogger<> _logger;

    public MELaboratoryReportController(
        ILogger<MELaboratoryReportController> logger,
        IReportAuthorizationService authorizationService,
        ILaboratoryQueryService laboratoryQueryService,
        IReportCacheService cacheService)
        : base(authorizationService, cacheService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _laboratoryQueryService = laboratoryQueryService ?? throw new ArgumentNullException(nameof(laboratoryQueryService));
    }

    /// <summary>
    /// 產生快取 Key 的查詢
    /// </summary>
    /// <param name="request">報表查詢篩選條件</param>
    [HttpPost("prepare-cache")]
    [Function("PrepareReportCache", "準備報表快取資料", Icon = "fa-solid fa-database", Order = 1, Description = "預先查詢實驗室報表數據並寫入快取，回傳快取識別碼以供後續下載使用")]
    [RequiresPermission("ORG.MELABORATORYREPORT.DOWNLOAD", "下載報表")]
    [EndpointSummary("準備報表快取資料")]
    [EndpointDescription("預先查詢實驗室報表數據並寫入快取，回傳快取識別碼以供後續下載使用。")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PrepareReportCache([FromBody] LaboratoryQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string targetLabId = HttpContext.Items["TenantLabId"]?.ToString()
                          ?? Request.Headers["X-Lab-Id"].FirstOrDefault()
                          ?? Request.Headers["TargetLabId"].FirstOrDefault()
                          ?? string.Empty;

        if (string.IsNullOrWhiteSpace(targetLabId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求數據",
                Detail = "缺少必要的實驗室上下文範圍 (TargetLabId)。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var reportData = await _laboratoryQueryService.GetReportDataAsync(User.Identity?.Name ?? "System", targetLabId, request);
            string cacheKey = await _cacheService.PrepareAndCacheReportAsync(reportData);

            return Ok(new { cacheKey });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "準備實驗室報表快取資料時發生非預期錯誤。TargetLabId: {TargetLabId}", targetLabId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "無法產生報表快取資料。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 取得特定報表類別之清單資料
    /// </summary>
    /// <param name="categoryCode">報表類別代碼</param>
    /// <param name="cancellationToken">取消權牌</param>
    [HttpGet("category/{categoryCode}")]
    [Function("GetReportsByCategory", "取得指定報表類別清單", Icon = "fa-solid fa-folder-open", Order = 2, Description = "透過動態規則引擎驗證報表類別權限後取得清單")]
    [RequireReportCategory("ENV", "ISO14064", "CARBON")]
    [RequiresPermission("ORG.MELABORATORYREPORT.READ")]
    [EndpointSummary("取得指定報表類別清單")]
    [EndpointDescription("透過動態規則引擎驗證報表類別權限後取得清單。")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReportsByCategoryAsync([FromRoute] string categoryCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryCode);

        try
        {
            await Task.CompletedTask;
            return Ok(new { success = true, category = categoryCode, message = "成功通過報表類別動態規則引擎驗證。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得報表類別清單時發生非預期錯誤，類別: {CategoryCode}", categoryCode);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = $"無法取得報表類別資料 (Category: {categoryCode})。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 下載實驗室列表 PDF 報表
    /// </summary>
    /// <param name="cacheKey">快取識別碼 (前端先行呼叫查詢 API 將資料寫入快取後取得)</param>
    [HttpGet("download")]
    [Function("DownloadLaboratoryListReport", "下載實驗室列表報表", Icon = "fa-solid fa-file-pdf", Order = 3, Description = "依據快取金鑰產生並下載 PDF 格式之實驗室清單報表")]
    [RequiresPermission("ORG.MELABORATORYREPORT.DOWNLOAD", "下載報表")]
    [EndpointSummary("下載實驗室列表報表")]
    [EndpointDescription("依據快取金鑰產生並下載 PDF 格式之實驗室清單報表。")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadLaboratoryListReport([FromQuery] string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求參數",
                Detail = "快取識別碼 (cacheKey) 不可為空。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            return await ExecuteDownloadPdfAsync(
                reportTypeCode: ReportTypeCode,
                cacheKey: cacheKey,
                fileDownloadName: "Laboratory_List_Report",
                reportGenerator: reportData =>
                {
                    var generator = new LaboratoryListReportGenerator(reportData.Details);
                    generator.SetReportData(reportData);
                    return generator.GeneratePdf();
                }
            );
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "下載實驗室報表失敗：找不到對應的快取資料。CacheKey: {CacheKey}", cacheKey);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "資源不存在",
                Detail = "報表快取資料已過期或不存在，請重新進行查詢。",
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下載實驗室列表報表時發生非預期錯誤。CacheKey: {CacheKey}", cacheKey);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "產生並下載報表檔案時發生錯誤。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}