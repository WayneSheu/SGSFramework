using System.Net.Mime;
using System.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.DataProtection.Abstractions;
using SGSFramework.DataProtection.DTOS;

namespace SGSFramework.ApiInfrastructure.Controllers;

/// <summary>
/// 系統資料保護與加解密管理控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/data-protections")]
[RequiresPermission("SYSTEM.DATAPROTECTION.READ")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
[ControllerTitle("資料保護管理", Icon = "fa-solid fa-shield-halved", Order = 23, Description = "提供系統敏感資料之加密與解密安全性服務")]
public sealed class DataProtectionController : ApiControllerBase
{
    private readonly IDiApi _diApi;
    private readonly ILogger<DataProtectionController> _logger;

    public DataProtectionController(
        IDiApi diApi,
        ILogger<DataProtectionController> logger)
    {
        ArgumentNullException.ThrowIfNull(diApi);
        ArgumentNullException.ThrowIfNull(logger);

        _diApi = diApi;
        _logger = logger;
    }

    /// <summary>
    /// 執行敏感資料加密
    /// </summary>
    /// <param name="request">加密請求資料內容</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>加密後之資料結果</returns>
    [HttpPost("encrypt")]
    [Function("EncryptData", "資料加密", Icon = "fa-solid fa-lock", Order = 1, Description = "根據指定的防護策略對敏感資料進行安全加密")]
    [RequiresPermission("SYSTEM.DATAPROTECTION.ENCRYPT")]
    
    [EndpointSummary("資料加密")]
    [EndpointDescription("根據傳入的防護策略將敏感資料進行安全加密。")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Encrypt(
        [FromBody] EncryptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var encryptedData = await _diApi.SecureProcessAsync(request.Payload, request.Strategy);
            _logger.LogInformation("資料加密處理成功。Strategy: {Strategy}", request.Strategy);

            return Ok(new { Data = encryptedData, Status = "Success" });
        }
        catch (SecurityException ex)
        {
            _logger.LogWarning(ex, "資料加密安全性驗證失敗。Strategy: {Strategy}", request.Strategy);
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "存取被拒絕",
                Detail = "安全防護處理失敗，存取權限不足或金鑰驗證失敗。",
                Instance = HttpContext.Request.Path
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("資料加密請求已被使用者取消。");
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行資料加密時發生非預期錯誤。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "執行資料加密時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 執行密文資料解密
    /// </summary>
    /// <param name="request">解密請求資料內容</param>
    /// <param name="cancellationToken">非同步取消權牌</param>
    /// <returns>解密後之原始資料結果</returns>
    [HttpPost("decrypt")]
    [Function("DecryptData", "資料解密", Icon = "fa-solid fa-key", Order = 2, Description = "根據指定的防護策略將加密文字安全解密")]
    [RequiresPermission("SYSTEM.DATAPROTECTION.DECRYPT")]
    
    [EndpointSummary("資料解密")]
    [EndpointDescription("根據傳入的防護策略將加密文字安全解密。")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Decrypt(
        [FromBody] DecryptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var decryptedData = await _diApi.SecureDecryptAsync(request.CipherText, request.Strategy);
            _logger.LogInformation("資料解密處理成功。Strategy: {Strategy}", request.Strategy);

            return Ok(new { Data = decryptedData, Status = "Success" });
        }
        catch (SecurityException ex)
        {
            _logger.LogWarning(ex, "資料解密安全性驗證失敗。Strategy: {Strategy}", request.Strategy);
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "存取被拒絕",
                Detail = "安全解密處理失敗，驗證金鑰不符或解密權限不足。",
                Instance = HttpContext.Request.Path
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("資料解密請求已被使用者取消。");
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行資料解密時發生非預期錯誤。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "執行資料解密時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}