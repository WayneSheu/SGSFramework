using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.CAL.Application.Contracts;
using SGS.Modules.CAL.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.CAL.Controllers.V1;

/// <summary>
/// 假日與補班日維護：公司工作日曆的唯一來源。PMS 的 SLA、合約工作日、加班判定等經 Core 的 IWorkCalendarProvider 讀取，
/// 修改假日不會回頭重算已分流問題單的期限。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/cal/holidays")]
[ControllerTitle("假日與補班", Icon = "fa-solid fa-calendar-check", Order = 2, Description = "國定假日、公司休假與補班日（工作日運算的唯一來源）")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class HolidaysController(HolidayAdminService holidays, GovCalendarSyncService govSync) : CalControllerBase
{
    /// <summary>查詢假日與補班日（可依年度）</summary>
    [HttpPost("search")]
    [Function("SearchHolidays", "假日與補班", Icon = "fa-solid fa-calendar-check", Order = 1, Description = "國定假日與補班日", IsMenu = true, Path = "/sgs.modules.cal/holidays")]
    // 選單頁面節點以權限碼為 Key：每個選單入口須使用各自獨立的權限碼
    [RequiresPermission("CAL.HOLIDAY.READ", "檢視假日與補班")]
    [ProducesResponseType(typeof(IReadOnlyList<HolidayDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HolidayDto>>> Search([FromBody] HolidaySearchRequest request, CancellationToken ct = default) =>
        Ok(await holidays.SearchAsync(request?.Year, ct).ConfigureAwait(false));

    /// <summary>批次新增或更新假日（同日期覆寫；單次最多 400 筆）</summary>
    [HttpPut]
    [Function("UpsertHolidays", "維護假日與補班", Order = 2, Description = "批次新增／更新假日與補班日")]
    [RequiresPermission("CAL.HOLIDAY.MANAGE", "維護假日與補班", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(UpsertHolidaysResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UpsertHolidaysResultDto>> Upsert([FromBody] UpsertHolidaysRequest request, CancellationToken ct = default) =>
        FromResult(await holidays.UpsertAsync(Actor, request, ct).ConfigureAwait(false));

    /// <summary>刪除假日</summary>
    [HttpDelete("{date}")]
    [Function("DeleteHoliday", "刪除假日", Order = 3, Description = "刪除指定日期的假日設定")]
    [RequiresPermission("CAL.HOLIDAY.MANAGE", "維護假日與補班", ActionCategory.Administrative)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(string date, CancellationToken ct = default)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed))
            return ProblemFrom(CalError.Validation("日期格式須為 yyyy-MM-dd。"));

        return NoContentFrom(await holidays.DeleteAsync(parsed, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// 與政府行政機關辦公日曆（行政院人事行政總處）同步：commit=false 預覽新增／更新／移除／人工衝突；commit=true 寫入並留同步紀錄。
    /// 只會覆寫與移除「同步來源」的資料；人工維護且內容不同的日子預設保留（overwriteManual=true 才覆寫）。
    /// </summary>
    [HttpPost("sync-gov")]
    [Function("SyncGovHolidays", "同步政府辦公日曆", Order = 5, Description = "從政府行政機關辦公日曆同步假日、補假與補班日（先預覽再寫入）")]
    [RequiresPermission("CAL.HOLIDAY.MANAGE", "維護假日與補班", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(GovSyncResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GovSyncResultDto>> SyncGov([FromBody] GovSyncRequest request, CancellationToken ct = default) =>
        FromResult(await govSync.SyncAsync(Actor, request, ct).ConfigureAwait(false));

    /// <summary>
    /// 政府辦公日曆自動同步（排程以系統帳號 scope: cal.holiday.sync 每月呼叫，亦可由具權限者手動執行）：同步當年與明年，
    /// 只新增與更新同步來源的資料，不覆寫人工維護；明年尚未公布不算失敗。
    /// </summary>
    [HttpPost("sync-gov/scan")]
    [Function("ScanGovHolidays", "自動同步政府辦公日曆", Order = 6, Description = "每月自動同步當年與明年的政府辦公日曆")]
    [RequiresSystemScope("cal.holiday.sync", Description = "自動同步政府行政機關辦公日曆", AllowUsers = true)]
    [RequiresPermission("CAL.HOLIDAY.MANAGE", "維護假日與補班", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(GovSyncScanResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GovSyncScanResultDto>> ScanGov(CancellationToken ct = default) =>
        Ok(await govSync.ScanAsync(ct).ConfigureAwait(false));

    /// <summary>最近的同步紀錄（預設 10 筆，最多 50）</summary>
    [HttpPost("sync-runs/search")]
    [RequiresPermission("CAL.HOLIDAY.READ", "檢視假日與補班")]
    [ProducesResponseType(typeof(IReadOnlyList<HolidaySyncRunDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HolidaySyncRunDto>>> SyncRuns([FromBody] HolidaySyncRunSearchRequest request, CancellationToken ct = default) =>
        Ok(await govSync.RecentRunsAsync(request?.Top, ct).ConfigureAwait(false));

    /// <summary>CSV／Tab 文字批次匯入：commit=false 只預覽，commit=true 且無格式錯誤才寫入</summary>
    [HttpPost("import")]
    [Function("ImportHolidays", "匯入假日", Order = 4, Description = "以 CSV 批次匯入假日與補班日（先預覽再確認）")]
    [RequiresPermission("CAL.HOLIDAY.MANAGE", "維護假日與補班", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(HolidayImportResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HolidayImportResultDto>> Import([FromBody] HolidayImportRequest request, CancellationToken ct = default) =>
        FromResult(await holidays.ImportAsync(Actor, request, ct).ConfigureAwait(false));
}
