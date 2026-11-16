using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Services;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Controllers.V1;

/// <summary>
/// 專案文件（交付物）：由 PMS 直接上傳至 FMS（加密儲存）並登記、浮水印檢視、授權下載、虛擬刪除。
/// 檔名由系統依 SOP 5.5 產生；檔案只能經上傳建立（不接受外部 FMS 檔案識別碼）。
/// 上傳需 <c>PMS.DELIVERABLE.UPLOAD</c>（與專案管理分離，讓 BE／FE 可上傳而不取得管理專案權限）；刪除需 <c>PMS.PROJECT.MANAGE</c> 且為專案 PM／Tech Lead。
/// 專案資料列授權（僅成員可見，非成員一律 404）由服務層套用。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/pms")]
[ControllerTitle("交付物管理", Icon = "fa-solid fa-folder-tree", Order = 33, Description = "專案文件上傳、浮水印檢視、下載與命名檢查")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class DeliverablesController(
    ProjectService projects,
    DeliverableFileService deliverables,
    TimeProvider timeProvider) : PmsControllerBase
{
    /// <summary>請求上限：檔案 50MB（對應 FMS 分類 pms-deliverable）加表單欄位與 multipart 開銷。</summary>
    private const long MaxUploadBytes = 52L * 1024 * 1024;

    private const int MaxFormFieldLength = 512;

    public const string ContentSha256Header = "X-Content-SHA256";

    /// <summary>檔名命名檢查（不寫入資料）</summary>
    [HttpPost("deliverables/naming-check")]
    [Function("CheckDeliverableNaming", "命名檢查", Order = 1, Description = "依 SOP 5.5 檢查檔名")]
    [RequiresPermission("PMS.PROJECT.READ", "檢視專案")]
    [ProducesResponseType(typeof(NamingCheckDto), StatusCodes.Status200OK)]
    public ActionResult<NamingCheckDto> NamingCheck([FromBody] NamingCheckRequest request) =>
        Ok(ProjectService.CheckNaming(request));

    /// <summary>
    /// 上傳交付物（multipart/form-data，串流不落地）：表單欄位 stage、docType、version、moduleId（選填）、docName（選填，預設取檔名）
    /// 須位於檔案區段之前；可附 X-Content-SHA256 標頭驗證傳輸完整性。系統依 SOP 5.5 產生標準檔名。
    /// </summary>
    [HttpPost("projects/{id:guid}/deliverables/upload")]
    [DisableFormValueModelBinding]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [Function("UploadDeliverable", "上傳交付物", Order = 2, Description = "上傳檔案至 FMS 並登記為專案交付物，檔名由系統依命名鐵律產生")]
    [RequiresPermission("PMS.DELIVERABLE.UPLOAD", "上傳交付物", ActionCategory.Operational)]
    [ProducesResponseType(typeof(DeliverableDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<DeliverableDto>> Upload(
        Guid id,
        [FromHeader(Name = ContentSha256Header)] string? contentSha256,
        CancellationToken ct = default)
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var mediaType) ||
            !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return ProblemFrom(new PmsError("PMS.FILE_REJECTED", 415, "Unsupported Media Type", "請以 multipart/form-data 上傳檔案。"));
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary))
            return ProblemFrom(PmsError.Validation("multipart 邊界 (boundary) 不正確。"));

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var reader = new MultipartReader(boundary, Request.Body);
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(ct).ConfigureAwait(false)) is not null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)) continue;

                if (disposition.IsFileDisposition())
                {
                    if (!TryBuildMeta(fields, contentSha256, out var meta, out var metaError))
                        return ProblemFrom(metaError!);

                    var fileName = disposition.FileNameStar.HasValue ? disposition.FileNameStar.Value : disposition.FileName.Value;
                    var result = await deliverables.UploadAsync(CurrentUserId, Actor, id, meta!, section.Body, fileName ?? string.Empty, ct).ConfigureAwait(false);
                    return FromResult(result, dto => Created($"/api/v1/pms/projects/{id}/deliverables", dto));
                }

                if (disposition.IsFormDisposition() && disposition.Name.Value is { Length: > 0 } name)
                {
                    fields[name] = await ReadFormFieldAsync(section, ct).ConfigureAwait(false);
                }
            }

            return ProblemFrom(PmsError.Validation("請選擇要上傳的檔案。"));
        }
        catch (InvalidDataException ex)
        {
            return ProblemFrom(PmsError.Validation($"multipart 內容格式不正確：{ex.Message}"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }

    /// <summary>查詢專案文件（預設僅各文件最新版，可含歷史版本）</summary>
    [HttpPost("projects/{id:guid}/deliverables/search")]
    [Function("SearchDeliverables", "查詢交付物", Order = 3, Description = "依階段、文件類型、模組查詢專案文件")]
    [RequiresPermission("PMS.PROJECT.READ", "檢視專案")]
    [ProducesResponseType(typeof(PagedResult<DeliverableDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DeliverableDto>>> Search(Guid id, [FromBody] DeliverableSearchRequest request, CancellationToken ct = default) =>
        FromResult(await deliverables.SearchAsync(CurrentUserId, id, request ?? new DeliverableSearchRequest(), ct, await HasPermissionAsync(ProjectAdminPermission, ct).ConfigureAwait(false)).ConfigureAwait(false));

    /// <summary>浮水印檢視（PDF／圖片）：浮水印為「使用者 | UTC 時間 | IP」，繪入像素；回應禁止快取</summary>
    [HttpGet("projects/{id:guid}/deliverables/{deliverableId:guid}/view")]
    [Produces(MediaTypeNames.Application.Pdf, "image/jpeg", "image/png")]
    [Function("ViewDeliverable", "檢視交付物（浮水印）", Order = 4, Description = "以動態浮水印線上檢視 PDF／圖片")]
    [RequiresPermission("PMS.PROJECT.READ", "檢視專案")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> View(Guid id, Guid deliverableId, CancellationToken ct = default)
    {
        var result = await deliverables.OpenViewAsync(CurrentUserId, id, deliverableId, BuildWatermarkText(timeProvider), ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        // 浮水印因人、因時而異：一律不可快取
        Response.Headers.CacheControl = "no-store";
        ApplySecurityHeaders();
        Response.Headers["Content-Digest"] = $"sha-256=:{Convert.ToBase64String(SHA256.HashData(result.Value!.Content))}:";
        return File(result.Value.Content, result.Value.ContentType);
    }

    /// <summary>下載原檔（需 PMS.DELIVERABLE.DOWNLOAD）：於記憶體串流解密後輸出</summary>
    [HttpGet("projects/{id:guid}/deliverables/{deliverableId:guid}/content")]
    [Produces(MediaTypeNames.Application.Octet)]
    [Function("DownloadDeliverable", "下載交付物", Order = 5, Description = "下載專案文件原檔")]
    [RequiresPermission("PMS.DELIVERABLE.DOWNLOAD", "下載交付物原檔", ActionCategory.Operational)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(Guid id, Guid deliverableId, CancellationToken ct = default)
    {
        var result = await deliverables.OpenDownloadAsync(CurrentUserId, id, deliverableId, ct).ConfigureAwait(false);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        var download = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        ApplySecurityHeaders();
        Response.Headers["Repr-Digest"] = $"sha-256=:{Convert.ToBase64String(Convert.FromHexString(download.Metadata.Sha256))}:";

        // FileStreamResult 以緩衝區非同步複製並於結束後釋放串流，不整檔讀入記憶體
        return File(download.Content!, download.Metadata.ContentType, download.Metadata.OriginalFileName);
    }

    /// <summary>刪除交付物（虛擬刪除，僅專案 PM／Tech Lead）</summary>
    [HttpDelete("projects/{id:guid}/deliverables/{deliverableId:guid}")]
    [Function("DeleteDeliverable", "刪除交付物", Order = 6, Description = "虛擬刪除專案文件，實體檔案於保留期滿後由排程清除")]
    [RequiresPermission("PMS.PROJECT.MANAGE", "管理專案", ActionCategory.Critical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, Guid deliverableId, CancellationToken ct = default) =>
        NoContentFrom(await deliverables.DeleteAsync(CurrentUserId, Actor, id, deliverableId, ct).ConfigureAwait(false));

    // ---------- 內部輔助 ----------

    private static bool TryBuildMeta(
        Dictionary<string, string> fields, string? sha256, out UploadDeliverableMeta? meta, out PmsError? error)
    {
        meta = null;
        error = null;

        if (!fields.TryGetValue("stage", out var stageText) || !Enum.TryParse<ProjectPhase>(stageText, ignoreCase: true, out var stage))
        {
            error = PmsError.Validation("stage 為必填（Initiation、Requirement、Design、Development、Testing 或 DeployClose）。");
            return false;
        }

        if (!fields.TryGetValue("docType", out var docType) || string.IsNullOrWhiteSpace(docType))
        {
            error = PmsError.Validation("docType 為必填。");
            return false;
        }

        if (!fields.TryGetValue("version", out var version) || string.IsNullOrWhiteSpace(version))
        {
            error = PmsError.Validation("version 為必填（例：v1.0）。");
            return false;
        }

        Guid? moduleId = null;
        if (fields.TryGetValue("moduleId", out var moduleText) && !string.IsNullOrWhiteSpace(moduleText))
        {
            if (!Guid.TryParse(moduleText, out var parsed))
            {
                error = PmsError.Validation("moduleId 格式不正確。");
                return false;
            }

            moduleId = parsed;
        }

        fields.TryGetValue("docName", out var docName);
        meta = new UploadDeliverableMeta(stage, docType, moduleId, version, docName, string.IsNullOrWhiteSpace(sha256) ? null : sha256.Trim());
        return true;
    }

    private static async Task<string> ReadFormFieldAsync(MultipartSection section, CancellationToken ct)
    {
        var buffer = new char[MaxFormFieldLength + 1];
        using var reader = new StreamReader(section.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
        if (read > MaxFormFieldLength) throw new InvalidDataException("表單欄位內容過長。");
        return new string(buffer, 0, read).Trim();
    }
}
