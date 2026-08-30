using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Reports.Dtos;
using SGS.Modules.ORG.Application.Reports.Generators;
using SGS.Modules.ORG.Application.Services;
using SGSFramework.AuthTokenBucket.Attributes;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.ReportEngine.Abstractions;
using SGSFramework.ReportEngine.Controllers;
using SGSFramework.ReportEngine.Security;

namespace SGS.Modules.ORG.Controllers
{
    /// <summary>
    /// ME實驗室報表控制器
    /// </summary>
    [ApiController]
    [ApiVersion("v1")]
    [Route("api/v1/reports/laboratories")]
    [ControllerTitle("ME實驗室報表管理", Icon = "fa-solid fa-flask", Order = 10, Description = "實驗室報表權限範例。")]
    [RequiresPermission("ORG_RPT_READ")]
    [RequireLaboratory("ME")]
    public class MELaboratoryReportController : ReportDownloadControllerBase<LaboratoryListReportDto>
    {
        private readonly ILogger<MELaboratoryReportController> _logger;
        private const string ReportTypeCode = "RPT_LAB_LIST";
        private readonly ILaboratoryQueryService _laboratoryQueryService;
        public MELaboratoryReportController(
            ILogger<MELaboratoryReportController> logger,
            IReportAuthorizationService authorizationService,
            ILaboratoryQueryService laboratoryQueryService,
            IReportCacheService cacheService)
            : base(authorizationService, cacheService)
        {
            _logger =  logger;
            _laboratoryQueryService= laboratoryQueryService;

        }

        /// <summary>
        /// 產生快取 Key」的查詢
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cacheService"></param>
        /// <param name="targetLabId"></param>
        /// <returns></returns>
        [HttpPost("prepare-cache")]
        public async Task<IActionResult> PrepareReportCache([FromBody] LaboratoryQueryRequest request)
        {
            // 優先從 HttpContext 取得系統已驗證解析的實驗室 ID，若無才 fallback 到 Header
            string targetLabId = HttpContext.Items["TenantLabId"]?.ToString()
                              ?? Request.Headers["X-Lab-Id"].FirstOrDefault()
                              ?? Request.Headers["TargetLabId"].FirstOrDefault()
                              ?? string.Empty;

            if (string.IsNullOrWhiteSpace(targetLabId))
            {
                return BadRequest(new { message = "缺少必要的實驗室上下文範圍 (TargetLabId)。" });
            }

            var reportData = await _laboratoryQueryService.GetReportDataAsync(User.Identity?.Name ?? "System", targetLabId, request);

            string cacheKey = await _cacheService.PrepareAndCacheReportAsync(reportData);

        ItemType: Ok(new { cacheKey });
            return Ok(new { cacheKey });
        }

        /// <summary>
        /// 取得特定報表類別之清單資料
        /// </summary>
        [HttpGet("category/{categoryCode}")]
        [Function("GetReportsByCategory", "取得指定報表類別清單", Icon = "fa-solid fa-folder-open", Order = 1, Description = "透過動態規則引擎驗證報表類別權限後取得清單")]
        [RequiresPermission("REPORT_MANAGE_READ")]
        [RequireReportCategory("ENV", "ISO14064", "CARBON")]
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
        public async Task<IActionResult> DownloadLaboratoryListReport([FromQuery] string cacheKey)
        {
            return await ExecuteDownloadPdfAsync(
                reportTypeCode: ReportTypeCode,
                cacheKey: cacheKey,
                fileDownloadName: "Laboratory_List_Report",
                reportGenerator: reportData =>
                {
                    // 執行具體的產生器實體並轉為 PDF bytes
                    var generator = new LaboratoryListReportGenerator(reportData.Details);
                    generator.SetReportData(reportData);
                    return generator.GeneratePdf();
                }
            );
        }
    }
}
