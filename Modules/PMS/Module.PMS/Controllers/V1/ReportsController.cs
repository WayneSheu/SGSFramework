using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Controllers.V1;

/// <summary>
/// 月度儀表板與月度維運報告（SOP 7.2）：總進線單數、SLA 準時達成率、MTTR、問題類型佔比；PM／Tech Lead 產生 PDF（FMS pms-form，加密儲存）。
/// 專案資料列授權：非專案內部成員一律 404。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms")]
[ControllerTitle("月度報告", Icon = "fa-solid fa-chart-column", Order = 40, Description = "月度維運儀表板與報告")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ReportsController(MonthlyReportService reports) : PmsControllerBase
{
    /// <summary>月度儀表板（即時重算；period 省略時為當月）</summary>
    [HttpGet("dashboard/monthly")]
    [Function("GetMonthlyDashboard", "月度儀表板", Order = 1, Description = "進線數、SLA 準時達成率、MTTR、類型佔比")]
    [RequiresPermission("PMS.REPORT.READ", "檢視月度報告")]
    [ProducesResponseType(typeof(MonthlyDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyDashboardDto>> Dashboard([FromQuery] Guid projectId, [FromQuery] string? period, CancellationToken ct = default) =>
        FromResult(await reports.GetDashboardAsync(CurrentUserId, projectId, period, ct).ConfigureAwait(false));

    /// <summary>查詢已產生的月報</summary>
    [HttpPost("reports/monthly/search")]
    [Function("SearchMonthlyReports", "月度維運報告", Icon = "fa-solid fa-chart-column", Order = 2, Description = "月度儀表板與已產生的月報", IsMenu = true, Path = "/sgs.modules.pms/monthly-reports")]
    [RequiresPermission("PMS.REPORT.READ", "檢視月度報告")]
    [ProducesResponseType(typeof(PagedResult<MonthlyReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MonthlyReportDto>>> Search([FromBody] MonthlyReportSearchRequest request, CancellationToken ct = default) =>
        Ok(await reports.SearchAsync(CurrentUserId, request ?? new MonthlyReportSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>產生月報 PDF（僅專案 PM／Tech Lead；重新產生會取代舊版）</summary>
    [HttpPost("reports/monthly/generate")]
    [Function("GenerateMonthlyReport", "產生月報 PDF", Order = 3, Description = "統計並產生月度維運報告 PDF")]
    [RequiresPermission("PMS.REPORT.GENERATE", "產生月度報告", ActionCategory.Operational)]
    [ProducesResponseType(typeof(MonthlyReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyReportDto>> Generate([FromBody] GenerateMonthlyReportRequest request, CancellationToken ct = default) =>
        FromResult(await reports.GenerateAsync(CurrentUserId, Actor, request, ct).ConfigureAwait(false));

    /// <summary>
    /// 自動產生月報（排程以系統帳號 scope: pms.report.monthly 每天呼叫；亦可由具權限者手動執行並以 period 回補指定月份）。
    /// 月底最後工作日產生當月、月初前 3 個工作日補產上月缺漏；只處理進行中且在維運期的專案；已有月報者不覆蓋。
    /// </summary>
    [HttpPost("reports/monthly/auto-generate")]
    [Function("AutoGenerateMonthlyReports", "自動產生月報", Order = 5, Description = "排程自動產生月度維運報告並通知 PM／Tech Lead")]
    [RequiresSystemScope("pms.report.monthly", Description = "自動產生月度維運報告", AllowUsers = true)]
    [RequiresPermission("PMS.SETTINGS.MANAGE", "維護 PMS 設定", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(MonthlyAutoGenerateResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyAutoGenerateResult>> AutoGenerate([FromQuery] string? period, CancellationToken ct = default) =>
        FromResult(await reports.AutoGenerateAsync(period, ct).ConfigureAwait(false));

    /// <summary>下載月報 PDF（專案內部成員；於記憶體串流解密後輸出）</summary>
    [HttpGet("reports/monthly/{id:guid}/content")]
    [Produces(MediaTypeNames.Application.Pdf)]
    [Function("DownloadMonthlyReport", "下載月報 PDF", Order = 4, Description = "下載月度維運報告 PDF")]
    [RequiresPermission("PMS.REPORT.READ", "檢視月度報告")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct = default)
    {
        var result = await reports.OpenDownloadAsync(CurrentUserId, id, ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        var download = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        ApplySecurityHeaders();
        return File(download.Content!, download.Metadata.ContentType, download.Metadata.OriginalFileName);
    }
}
