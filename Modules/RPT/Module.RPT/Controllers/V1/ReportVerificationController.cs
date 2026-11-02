using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.RPT.Application.Dtos;
using SGS.Modules.RPT.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;

namespace SGS.Modules.RPT.Controllers.V1;

/// <summary>
/// 報告真偽公開查驗（免登入）：以報告上的查驗碼查詢發行狀態，或上傳 PDF 比對 SHA-256 確認未遭變造。
/// 僅回傳報告編號、類別、實驗室、狀態與簽署資訊，不含客戶與檢驗數據。
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/report-verification")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ReportVerificationController(IReportService reportService) : ControllerBase
{
    /// <summary>單一 PDF 上傳上限</summary>
    private const long MaxUploadBytes = 20 * 1024 * 1024;

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    private readonly IReportService _service = reportService ?? throw new ArgumentNullException(nameof(reportService));

    /// <summary>以查驗碼查詢報告發行狀態</summary>
    [HttpGet("{token}")]
    [ProducesResponseType(typeof(ReportVerificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Verify([FromRoute] string token, CancellationToken cancellationToken = default)
    {
        var result = await _service.VerifyAsync(token, null, cancellationToken).ConfigureAwait(false);
        return result is null ? NotFoundResult() : Ok(result);
    }

    /// <summary>上傳 PDF 比對雜湊（僅接受真實 PDF 檔頭）</summary>
    [HttpPost("{token}/file")]
    [RequestSizeLimit(MaxUploadBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ReportVerificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyFile([FromRoute] string token, IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0 || file.Length > MaxUploadBytes)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "無效的檔案", Detail = "請上傳 20MB 以內的報告 PDF。" });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var content = buffer.ToArray();

        // 不以副檔名判斷，檢查檔頭 Magic Number
        if (content.Length < PdfMagic.Length || !content.AsSpan(0, PdfMagic.Length).SequenceEqual(PdfMagic))
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "檔案格式不符", Detail = "上傳檔案不是有效的 PDF。" });
        }

        var result = await _service.VerifyAsync(token, content, cancellationToken).ConfigureAwait(false);
        return result is null ? NotFoundResult() : Ok(result);
    }

    private NotFoundObjectResult NotFoundResult() =>
        NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "查無報告", Detail = "查驗碼不正確，或報告不存在。" });
}
