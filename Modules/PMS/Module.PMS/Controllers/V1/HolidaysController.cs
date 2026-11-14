using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.PMS.Controllers.V1;

/// <summary>國定假日／補班日維護：SLA 工作天計算（補 SOP 附錄 B-3 公式未涵蓋假日的缺口）。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms/holidays")]
[ControllerTitle("維運設定", Icon = "fa-solid fa-calendar-days", Order = 35, Description = "國定假日與補班日（SLA 工作天計算）")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class HolidaysController(HolidayService holidays) : PmsControllerBase
{
    /// <summary>查詢假日（可依年度）</summary>
    [HttpPost("search")]
    [Function("SearchHolidays", "國定假日", Icon = "fa-solid fa-calendar-days", Order = 1, Description = "國定假日與補班日，供 SLA 工作天計算", IsMenu = true, Path = "/sgs.modules.pms/holidays")]
    // 選單頁面節點以權限碼為 Key：每個選單入口須使用各自獨立的權限碼，否則會共用同一個選單節點而互相覆蓋
    // （原與問題單清單共用 PMS.TICKET.READ，導致「問題單清單」選單消失）。
    [RequiresPermission("PMS.SETTINGS.READ", "檢視 PMS 設定")]
    [ProducesResponseType(typeof(IReadOnlyList<HolidayDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HolidayDto>>> Search([FromBody] HolidaySearchRequest request, CancellationToken ct = default) =>
        Ok(await holidays.SearchAsync(request?.Year, ct).ConfigureAwait(false));

    /// <summary>批次新增或更新假日（同日期覆寫）</summary>
    [HttpPut]
    [Function("UpsertHolidays", "維護國定假日", Order = 2, Description = "批次新增／更新假日與補班日")]
    [RequiresPermission("PMS.SETTINGS.MANAGE", "維護 PMS 設定", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(UpsertHolidaysResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UpsertHolidaysResultDto>> Upsert([FromBody] UpsertHolidaysRequest request, CancellationToken ct = default) =>
        FromResult(await holidays.UpsertAsync(Actor, request, ct).ConfigureAwait(false));

    /// <summary>刪除假日</summary>
    [HttpDelete("{date}")]
    [Function("DeleteHoliday", "刪除國定假日", Order = 3, Description = "刪除指定日期的假日設定")]
    [RequiresPermission("PMS.SETTINGS.MANAGE", "維護 PMS 設定", ActionCategory.Administrative)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(string date, CancellationToken ct = default)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed))
            return ProblemFrom(PmsError.Validation("日期格式須為 yyyy-MM-dd。"));

        return NoContentFrom(await holidays.DeleteAsync(parsed, ct).ConfigureAwait(false));
    }
}
