using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using SGS.Modules.MTG.Application.Contracts;
using SGS.Modules.MTG.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.MTG.Controllers.V1;

/// <summary>
/// 會議錄音：上傳後以 AES-256-GCM 加密存入檔案管理（FMS），再由背景工作者交給本機轉錄服務轉成逐字稿。
/// 轉錄為非同步作業：上傳後會議進入「音檔轉錄中」，完成後回到「逐字稿就緒」。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/mtg/meetings/{meetingId:guid}/audio")]
[ControllerTitle("會議音檔", Icon = "fa-solid fa-file-audio", Order = 4, Description = "會議錄音上傳、轉錄與管理")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class MeetingAudioController(MeetingAudioService audios) : MtgControllerBase
{
    public const string ContentSha256Header = "X-Content-SHA256";

    /// <summary>單檔上傳上限（含 multipart 開銷）；須大於 FMS 分類 meeting-audio 與 Meeting:Whisper:MaxAudioBytes。</summary>
    private const long MaxUploadBytes = 101L * 1024 * 1024;

    private const int MaxFormFieldLength = 256;

    /// <summary>列出會議的音檔與轉錄狀態</summary>
    [HttpGet]
    [Function("ListMeetingAudio", "音檔清單", Order = 1, Description = "列出會議音檔與轉錄狀態")]
    [RequiresPermission("MTG.MEETING.READ", "檢視會議")]
    [ProducesResponseType(typeof(IReadOnlyList<MeetingAudioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MeetingAudioDto>>> List(Guid meetingId, CancellationToken ct = default) =>
        FromResult(await audios.ListAsync(meetingId, ct).ConfigureAwait(false));

    /// <summary>
    /// 上傳音檔（multipart/form-data，串流不落地）：表單欄位 autoSummarize（true／false）與 scenarioCode 須位於檔案區段之前；
    /// 可附 X-Content-SHA256 標頭驗證傳輸完整性。受理後回 202，轉錄在背景進行。
    /// </summary>
    [HttpPost]
    [DisableFormValueModelBinding]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [Function("UploadMeetingAudio", "上傳會議音檔", Order = 2, Description = "上傳錄音至 FMS（加密）並排入轉錄")]
    [RequiresPermission("MTG.AUDIO.UPLOAD", "上傳會議音檔", ActionCategory.Operational)]
    [ProducesResponseType(typeof(MeetingAudioDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<MeetingAudioDto>> Upload(
        Guid meetingId, [FromHeader(Name = ContentSha256Header)] string? contentSha256, CancellationToken ct = default)
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var mediaType) ||
            !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return ProblemFrom(MtgErrors.FileRejected(415, "請以 multipart/form-data 上傳檔案。"));
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary)) return ProblemFrom(MtgErrors.Validation("multipart 邊界 (boundary) 不正確。"));

        var autoSummarize = false;
        string? scenarioCode = null;
        try
        {
            var reader = new MultipartReader(boundary, Request.Body);
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(ct).ConfigureAwait(false)) is not null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)) continue;

                if (disposition.IsFileDisposition())
                {
                    var fileName = disposition.FileNameStar.HasValue ? disposition.FileNameStar.Value : disposition.FileName.Value;
                    var result = await audios.UploadAsync(
                        Actor, meetingId, section.Body, fileName ?? string.Empty,
                        string.IsNullOrWhiteSpace(contentSha256) ? null : contentSha256.Trim(), autoSummarize, scenarioCode, ct).ConfigureAwait(false);
                    return result.IsSuccess ? Accepted(result.Value) : ProblemFrom(result.Error!);
                }

                if (!disposition.IsFormDisposition()) continue;
                var value = await ReadFormFieldAsync(section, ct).ConfigureAwait(false);
                switch (disposition.Name.Value?.ToLowerInvariant())
                {
                    case "autosummarize": autoSummarize = bool.TryParse(value, out var b) && b; break;
                    case "scenariocode": scenarioCode = string.IsNullOrWhiteSpace(value) ? null : value; break;
                }
            }

            return ProblemFrom(MtgErrors.Validation("請選擇要上傳的音檔。"));
        }
        catch (InvalidDataException ex)
        {
            return ProblemFrom(MtgErrors.Validation($"multipart 內容格式不正確：{ex.Message}"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }

    /// <summary>重新轉錄（沿用已存放的音檔；成功後取代此音檔先前產生的片段）</summary>
    [HttpPost("{audioId:guid}/retranscribe")]
    [Function("RetranscribeMeetingAudio", "重新轉錄音檔", Order = 3, Description = "重新轉錄已上傳的音檔")]
    [RequiresPermission("MTG.AUDIO.UPLOAD", "上傳會議音檔", ActionCategory.Operational)]
    [ProducesResponseType(typeof(MeetingAudioDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<MeetingAudioDto>> Retranscribe(Guid meetingId, Guid audioId, CancellationToken ct = default)
    {
        var result = await audios.RetranscribeAsync(Actor, meetingId, audioId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Accepted(result.Value) : ProblemFrom(result.Error!);
    }

    /// <summary>下載音檔原檔（記憶體內解密；寫入 FMS 存取稽核）</summary>
    [HttpGet("{audioId:guid}/content")]
    [Produces(MediaTypeNames.Application.Octet)]
    [Function("DownloadMeetingAudio", "下載會議音檔", Order = 4, Description = "下載會議錄音原檔")]
    [RequiresPermission("MTG.AUDIO.DOWNLOAD", "下載會議音檔", ActionCategory.Critical)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(Guid meetingId, Guid audioId, CancellationToken ct = default)
    {
        var result = await audios.OpenDownloadAsync(meetingId, audioId, ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        var download = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(download.Content!, download.Metadata.ContentType, download.Metadata.OriginalFileName);
    }

    /// <summary>刪除音檔（FMS 虛擬刪除）；removeTranscript=true 時一併移除由此音檔轉出的逐字稿片段</summary>
    [HttpDelete("{audioId:guid}")]
    [Function("DeleteMeetingAudio", "刪除會議音檔", Order = 5, Description = "刪除會議錄音")]
    [RequiresPermission("MTG.AUDIO.DELETE", "刪除會議音檔", ActionCategory.Critical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid meetingId, Guid audioId, [FromQuery] bool removeTranscript = false, CancellationToken ct = default) =>
        NoContentFrom(await audios.DeleteAsync(Actor, meetingId, audioId, removeTranscript, ct).ConfigureAwait(false));

    private static async Task<string> ReadFormFieldAsync(MultipartSection section, CancellationToken ct)
    {
        var buffer = new char[MaxFormFieldLength + 1];
        using var reader = new StreamReader(section.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
        if (read > MaxFormFieldLength) throw new InvalidDataException("表單欄位內容過長。");
        return new string(buffer, 0, read).Trim();
    }
}
