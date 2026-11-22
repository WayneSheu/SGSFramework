using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.MTG.Application.Contracts;
using SGS.Modules.MTG.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.MTG.Controllers.V1;

/// <summary>
/// 會議管理：建立會議、貼上逐字稿（自動套用術語校正）、依場景產生 AI 會議紀要。
/// 紀要產生為非同步作業：要求後會議進入「產生中」，完成後可查詢紀要。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/mtg/meetings")]
[ControllerTitle("會議管理", Icon = "fa-solid fa-microphone-lines", Order = 1, Description = "會議、逐字稿與 AI 會議紀要")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class MeetingsController(MeetingService meetings) : MtgControllerBase
{
    /// <summary>分頁查詢會議</summary>
    [HttpPost("search")]
    [Function("SearchMeetings", "會議管理", Icon = "fa-solid fa-microphone-lines", Order = 1, Description = "會議清單與處理狀態", IsMenu = true, Path = "/sgs.modules.mtg/meetings")]
    [RequiresPermission("MTG.MEETING.READ", "檢視會議")]
    [ProducesResponseType(typeof(PagedResult<MeetingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MeetingDto>>> Search([FromBody] MeetingQueryRequest request, CancellationToken ct = default) =>
        Ok(await meetings.SearchAsync(request, ct).ConfigureAwait(false));

    /// <summary>取得單一會議</summary>
    [HttpGet("{id:guid}")]
    [Function("GetMeeting", "取得會議", Order = 2, Description = "取得單一會議資訊與狀態")]
    [RequiresPermission("MTG.MEETING.READ", "檢視會議")]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MeetingDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await meetings.GetAsync(id, ct).ConfigureAwait(false));

    /// <summary>建立會議</summary>
    [HttpPost]
    [Function("CreateMeeting", "建立會議", Order = 3, Description = "建立會議（草稿）")]
    [RequiresPermission("MTG.MEETING.CREATE", "建立會議", ActionCategory.Operational)]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MeetingDto>> Create([FromBody] SaveMeetingRequest request, CancellationToken ct = default) =>
        FromResult(await meetings.CreateAsync(Actor, request, ct).ConfigureAwait(false));

    /// <summary>更新會議</summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateMeeting", "更新會議", Order = 4, Description = "更新會議標題、場景與時間")]
    [RequiresPermission("MTG.MEETING.UPDATE", "修改會議", ActionCategory.Operational)]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MeetingDto>> Update(Guid id, [FromBody] SaveMeetingRequest request, CancellationToken ct = default) =>
        FromResult(await meetings.UpdateAsync(Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>刪除會議（連同逐字稿與紀要）</summary>
    [HttpDelete("{id:guid}")]
    [Function("DeleteMeeting", "刪除會議", Order = 5, Description = "刪除會議及其逐字稿、紀要版本")]
    [RequiresPermission("MTG.MEETING.DELETE", "刪除會議", ActionCategory.Critical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default) =>
        NoContentFrom(await meetings.DeleteAsync(id, ct).ConfigureAwait(false));

    /// <summary>貼上逐字稿（Replace=true 覆蓋，否則附加）；自動套用術語表校正</summary>
    [HttpPost("{id:guid}/transcript")]
    [Function("PasteMeetingTranscript", "貼上逐字稿", Order = 6, Description = "貼上逐字稿並套用術語校正")]
    [RequiresPermission("MTG.MEETING.UPDATE", "修改會議", ActionCategory.Operational)]
    [ProducesResponseType(typeof(PasteTranscriptResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PasteTranscriptResult>> PasteTranscript(Guid id, [FromBody] PasteTranscriptRequest request, CancellationToken ct = default) =>
        FromResult(await meetings.PasteTranscriptAsync(Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>
    /// 桌面端批次上傳逐字稿片段（即時收音的會議邊錄邊傳）：以 clientSegmentId 冪等，重送不會重複寫入；單次最多 500 筆。
    /// </summary>
    [HttpPost("{id:guid}/segments")]
    [Function("UploadMeetingSegments", "上傳逐字稿片段", Order = 11, Description = "桌面端批次上傳逐字稿片段（冪等）")]
    [RequiresPermission("MTG.CLIENT.UPLOAD", "桌面端上傳逐字稿", ActionCategory.Operational)]
    [ProducesResponseType(typeof(UploadSegmentsResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<UploadSegmentsResult>> UploadSegments(Guid id, [FromBody] UploadSegmentsRequest request, CancellationToken ct = default) =>
        FromResult(await meetings.AddSegmentsAsync(Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>桌面端結束會議：確認已有逐字稿，並依要求接著依場景產生紀要（非同步，回 202）</summary>
    [HttpPost("{id:guid}/finalize")]
    [Function("FinalizeMeeting", "結束會議", Order = 12, Description = "桌面端結束會議並要求產生紀要")]
    [RequiresPermission("MTG.CLIENT.UPLOAD", "桌面端上傳逐字稿", ActionCategory.Operational)]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<MeetingDto>> Finalize(Guid id, [FromBody] FinalizeMeetingRequest? request, CancellationToken ct = default)
    {
        var result = await meetings.FinalizeAsync(Actor, id, request, ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);
        return request?.GenerateMinutes == false ? Ok(result.Value) : Accepted(result.Value);
    }

    /// <summary>讀取逐字稿（含敏感內容，需獨立授權）</summary>
    [HttpGet("{id:guid}/transcript")]
    [Function("GetMeetingTranscript", "檢視逐字稿", Order = 7, Description = "檢視會議逐字稿")]
    [RequiresPermission("MTG.TRANSCRIPT.READ", "檢視逐字稿", ActionCategory.Critical)]
    [ProducesResponseType(typeof(IReadOnlyList<TranscriptSegmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TranscriptSegmentDto>>> GetTranscript(Guid id, CancellationToken ct = default) =>
        FromResult(await meetings.GetTranscriptAsync(id, ct).ConfigureAwait(false));

    /// <summary>要求產生 AI 紀要（非同步；可指定場景覆蓋會議預設）</summary>
    [HttpPost("{id:guid}/minutes")]
    [Function("GenerateMeetingMinutes", "產生會議紀要", Order = 8, Description = "依場景以本機模型產生會議紀要（非同步）")]
    [RequiresPermission("MTG.MINUTES.GENERATE", "產生會議紀要", ActionCategory.Operational)]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<MeetingDto>> GenerateMinutes(Guid id, [FromBody] GenerateMinutesRequest? request, CancellationToken ct = default)
    {
        var result = await meetings.RequestMinutesAsync(Actor, id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Accepted(result.Value) : ProblemFrom(result.Error!);
    }

    /// <summary>列出紀要版本</summary>
    [HttpGet("{id:guid}/minutes")]
    [Function("ListMeetingMinutes", "紀要版本清單", Order = 9, Description = "列出會議的紀要版本")]
    [RequiresPermission("MTG.MINUTES.READ", "檢視會議紀要")]
    [ProducesResponseType(typeof(IReadOnlyList<MinutesSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MinutesSummaryDto>>> ListMinutes(Guid id, CancellationToken ct = default) =>
        FromResult(await meetings.ListMinutesAsync(id, ct).ConfigureAwait(false));

    /// <summary>取得紀要內容（未指定版本時為最新版）</summary>
    [HttpGet("{id:guid}/minutes/latest")]
    [Function("GetMeetingMinutes", "檢視會議紀要", Order = 10, Description = "取得會議紀要 Markdown 內容")]
    [RequiresPermission("MTG.MINUTES.READ", "檢視會議紀要")]
    [ProducesResponseType(typeof(MinutesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MinutesDto>> GetMinutes(Guid id, [FromQuery] int? version, CancellationToken ct = default) =>
        FromResult(await meetings.GetMinutesAsync(id, version, ct).ConfigureAwait(false));
}
