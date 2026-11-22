using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.MTG.Application.Contracts;
using SGS.Modules.MTG.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.MTG.Controllers.V1;

/// <summary>會議錄音保存期限維護（排程用）。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/mtg/audio")]
[ControllerTitle("音檔保存期限", Icon = "fa-solid fa-hourglass-end", Order = 9, Description = "清除超過保存期限的會議錄音")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class AudioRetentionController(AudioRetentionService retention) : MtgControllerBase
{
    /// <summary>
    /// 清除超過保存期限（<c>Meeting:AudioRetentionDays</c>）的會議錄音：從檔案管理刪除，逐字稿與紀要保留。
    /// 排程以系統帳號 scope: mtg.audio.purge 每日呼叫，亦可由具權限者手動執行。
    /// </summary>
    [HttpPost("purge-expired")]
    [Function("PurgeExpiredMeetingAudio", "清除過期錄音", Order = 1, Description = "刪除超過保存期限的會議錄音")]
    [RequiresSystemScope("mtg.audio.purge", Description = "清除超過保存期限的會議錄音", AllowUsers = true)]
    [RequiresPermission("MTG.AUDIO.PURGE", "清除過期會議錄音", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(AudioPurgeResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<AudioPurgeResult>> PurgeExpired(CancellationToken ct = default) =>
        Ok(await retention.PurgeExpiredAsync(ct).ConfigureAwait(false));
}
