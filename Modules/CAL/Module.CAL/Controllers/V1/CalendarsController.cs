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
/// 公司行事曆：月曆檢視（活動、假日、補班日）與活動維護。全公司日曆任何具檢視權限者可讀；
/// 活動的新增、修改、刪除僅限具 CAL.EVENT.MANAGE 的特定管理員（一般員工唯讀，防止誤刪重要公告）。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/cal/calendar")]
[ControllerTitle("公司行事曆", Icon = "fa-solid fa-calendar-days", Order = 1, Description = "全公司共用行事曆：假日、補班日、公司大會與教育訓練")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class CalendarsController(CalendarService calendars, EventReminderService reminders) : CalControllerBase
{
    /// <summary>查詢期間內的日曆活動與假日／補班日（區間最長 62 天）</summary>
    [HttpPost("search")]
    [Function("SearchCalendar", "公司行事曆", Icon = "fa-solid fa-calendar-days", Order = 1, Description = "月曆檢視：活動、假日與補班日", IsMenu = true, Path = "/sgs.modules.cal/calendar")]
    [RequiresPermission("CAL.CALENDAR.READ", "檢視公司行事曆")]
    [ProducesResponseType(typeof(CalendarViewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarViewDto>> Search([FromBody] CalendarViewRequest request, CancellationToken ct = default) =>
        FromResult(await calendars.ViewAsync(request, ct).ConfigureAwait(false));

    /// <summary>新增日曆活動</summary>
    [HttpPost("events")]
    [Function("CreateCalendarEvent", "新增行事曆活動", Order = 2, Description = "新增公司大會、教育訓練、節慶活動等")]
    [RequiresPermission("CAL.EVENT.MANAGE", "維護行事曆活動", ActionCategory.Operational)]
    [ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarEventDto>> Create([FromBody] UpsertCalendarEventRequest request, CancellationToken ct = default) =>
        FromResult(await calendars.CreateAsync(CurrentUserId, Actor, request, ct).ConfigureAwait(false));

    /// <summary>更新日曆活動（外部系統同步的活動不可修改）</summary>
    [HttpPut("events/{id:guid}")]
    [Function("UpdateCalendarEvent", "更新行事曆活動", Order = 3, Description = "修改活動的時間、地點與說明")]
    [RequiresPermission("CAL.EVENT.MANAGE", "維護行事曆活動", ActionCategory.Operational)]
    [ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarEventDto>> Update(Guid id, [FromBody] UpsertCalendarEventRequest request, CancellationToken ct = default) =>
        FromResult(await calendars.UpdateAsync(Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>
    /// 派送活動提醒（排程以系統帳號 scope: cal.reminders.dispatch 每 5 分鐘呼叫，亦可由具權限者手動執行）：
    /// 寄 Email 給活動建立者與與會者；每組提醒只寄一次，活動開始後不再提醒。
    /// </summary>
    [HttpPost("reminders/dispatch")]
    [Function("DispatchCalendarReminders", "派送活動提醒", Order = 5, Description = "掃描到期的活動提醒並寄送 Email")]
    [RequiresSystemScope("cal.reminders.dispatch", Description = "派送行事曆活動提醒", AllowUsers = true)]
    [RequiresPermission("CAL.EVENT.MANAGE", "維護行事曆活動", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ReminderDispatchResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReminderDispatchResultDto>> DispatchReminders(CancellationToken ct = default) =>
        Ok(await reminders.DispatchAsync(ct).ConfigureAwait(false));

    /// <summary>刪除日曆活動</summary>
    [HttpDelete("events/{id:guid}")]
    [Function("DeleteCalendarEvent", "刪除行事曆活動", Order = 4, Description = "刪除活動")]
    [RequiresPermission("CAL.EVENT.MANAGE", "維護行事曆活動", ActionCategory.Operational)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default) =>
        NoContentFrom(await calendars.DeleteAsync(id, ct).ConfigureAwait(false));
}
