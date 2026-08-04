using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Utilities;
using SGSFramework.ApiInfrastructure.Controllers.DiApis.DTOs;
using SGSFramework.DataProtection.Abstractions;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;


namespace SGSFramework.ApiInfrastructure.Controllers.DiApis
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class DataProtectionController : ControllerBase
    {
        private readonly IDiApi _diApi;
        private readonly ILogger<DataProtectionController> _logger;

        public DataProtectionController(IDiApi diApi, ILogger<DataProtectionController> logger)
        {
            _diApi = diApi;
            _logger = logger;
        }

        [HttpPost("encrypt")]
        public async Task<IActionResult> Encrypt([FromBody] EncryptionRequest request)
        {
            // 1. 基本請求驗證 (由 Data Annotation 或 FluentValidation 處理)
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                // 2. 委派處理至 DIAPI
                var encryptedData = await _diApi.SecureProcessAsync(request.Payload, request.Strategy);

                // 3. 回傳標準回應
                return Ok(new { Data = encryptedData, Status = "Success" });
            }
            catch (SecurityException ex)
            {
                // 4. 針對安全性異常進行特殊記錄，但不洩漏過多細節給前端
                _logger.LogWarning("Security violation: {Message}", ex.Message);
                return StatusCode(StatusCodes.Status403Forbidden, "Secure processing failed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in encryption endpoint.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal system error.");
            }
        }

        [HttpPost("decrypt")]
        public async Task<IActionResult> Decrypt([FromBody] DecryptionRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                // 呼叫 IDiApi 的解密功能
                // 假設您在 IDiApi 中擴充了 SecureDecryptAsync 方法
                var decryptedData = await _diApi.SecureDecryptAsync(request.CipherText, request.Strategy);

                return Ok(new { Data = decryptedData, Status = "Success" });
            }
            catch (SecurityException ex)
            {
                _logger.LogWarning("Decryption security failure: {Message}", ex.Message);
                return StatusCode(StatusCodes.Status403Forbidden, "Decryption failed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "System error during decryption.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal system error.");
            }
        }
    }
}
