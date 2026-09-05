using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using SGSFramework.VerifyLedger.Dtos;
using SGSFramework.VerifyLedger.Reports;
using SGSFramework.VerifyLedger.Services;
using System.Reflection;
using System.Text.Json;

namespace SGSFramework.VerifyLedger.Controllers
{
    /// <summary>
    /// 泛型總帳驗證控制器，支援動態路由解析特定 DbContext 與 Entity
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/ledger")]
    [ControllerTitle("總帳驗證管理", Icon = "fa-solid fa-user-shield", Order = 20, Description = "泛型總帳驗證控制器，支援動態路由解析特定 DbContext 與 Entity")]
    [RequiresPermission("SYSTEM.LEDGERVERIFICATION.READ")]
    public class LedgerVerificationController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LedgerVerificationController> _logger;

        public LedgerVerificationController(
            IServiceProvider serviceProvider,
            ILogger<LedgerVerificationController> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 動態驗證指定資料庫與實體的總帳完整性
        /// </summary>
        [HttpPost("{contextName}/verify/{entityName}")]
        [Function("VerifyLedger", "帳本驗證", Icon = "fa-solid fa-shield-halved", Order = 1, Description = "動態驗證指定資料庫內容與實體的總帳完整性雜湊值")]
        [RequiresPermission("SYSTEM.LEDGERVERIFICATION.VERIFYLEDGER")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> VerifyLedgerAsync(string contextName, string entityName)
        {
            if (string.IsNullOrWhiteSpace(contextName) || string.IsNullOrWhiteSpace(entityName))
            {
                return BadRequest("資料庫內容名稱與實體名稱不得為空。");
            }

            try
            {
                var contextType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .FirstOrDefault(t => typeof(DbContext).IsAssignableFrom(t) &&
                                         (t.Name.Equals(contextName, StringComparison.OrdinalIgnoreCase) ||
                                          t.Name.Equals($"{contextName}DbContext", StringComparison.OrdinalIgnoreCase)));

                if (contextType == null)
                {
                    _logger.LogWarning("找不到指定的 DbContext 型別: {ContextName}", contextName);
                    return NotFound($"系統中找不到指定的資料庫內容控制項: {contextName}");
                }

                var entityType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .FirstOrDefault(t => typeof(ILedgerEntity).IsAssignableFrom(t) &&
                                         t.IsClass &&
                                         !t.IsAbstract &&
                                         t.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

                if (entityType == null)
                {
                    _logger.LogWarning("找不到指定的 Ledger 實體型別: {EntityName}", entityName);
                    return NotFound($"系統中找不到指定或未實作 ILedgerEntity 的實體: {entityName}");
                }

                var openServiceType = typeof(ILedgerVerificationService<,>);
                var closedServiceType = openServiceType.MakeGenericType(contextType, entityType);
                var service = _serviceProvider.GetService(closedServiceType);

                if (service == null)
                {
                    _logger.LogError("無法從 DI 容器解析服務: {ServiceType}", closedServiceType.FullName);
                    return StatusCode(StatusCodes.Status500InternalServerError, "對應的總帳驗證核心服務未註冊。");
                }

                using var reader = new StreamReader(Request.Body);
                var digestJson = await reader.ReadToEndAsync();
                var finalDigest = string.IsNullOrWhiteSpace(digestJson) ? null : digestJson;

                var method = closedServiceType.GetMethod(nameof(ILedgerVerificationService<DbContext, ILedgerEntity>.VerifyLedgerAsync));
                if (method == null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "無法載入驗證核心方法定義。");
                }

                var task = (Task)method.Invoke(service, [finalDigest])!;
                await task;

                var resultProperty = task.GetType().GetProperty("Result");
                var verificationResult = resultProperty?.GetValue(task);

                return Ok(verificationResult);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                _logger.LogError(ex.InnerException, "執行總帳驗證時核心底層拋出異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = ex.InnerException.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "路由 {ContextName}/{EntityName} 總帳驗證請求處理失敗。", contextName, entityName);
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "通用泛型總帳控制器處理常式錯誤", Detail = ex.Message });
            }
        }

        /// <summary>
        /// 驗證總帳並直接下載 PDF 稽核報告（支援完整動態淬取資料庫與資料表名稱）
        /// </summary>
        [HttpGet("{contextName}/report/{entityName}")]
        [Function("DownloadLedgerReport", "帳本驗證報告", Icon = "fa-solid fa-file-pdf", Order = 2, Description = "驗證指定資料庫實體之總帳並直接產生下載 PDF 稽核報告")]
        [RequiresPermission("SYSTEM.LEDGERVERIFICATION.DOWNLOADREPORT")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileResult))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadReport(string contextName, string entityName)
        {
            if (string.IsNullOrWhiteSpace(contextName) || string.IsNullOrWhiteSpace(entityName))
            {
                return BadRequest("資料庫內容名稱與實體名稱不得為空。");
            }

            try
            {
                var contextType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .FirstOrDefault(t => typeof(DbContext).IsAssignableFrom(t) &&
                                         (t.Name.Equals(contextName, StringComparison.OrdinalIgnoreCase) ||
                                          t.Name.Equals($"{contextName}DbContext", StringComparison.OrdinalIgnoreCase)));

                if (contextType == null)
                {
                    _logger.LogWarning("找不到指定的 DbContext 型別: {ContextName}", contextName);
                    return NotFound($"系統中找不到指定的資料庫內容控制項: {contextName}");
                }

                var entityType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .FirstOrDefault(t => typeof(ILedgerEntity).IsAssignableFrom(t) &&
                                         t.IsClass &&
                                         !t.IsAbstract &&
                                         t.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

                if (entityType == null)
                {
                    _logger.LogWarning("找不到指定的 Ledger 實體型別: {EntityName}", entityName);
                    return NotFound($"系統中找不到指定或未實作 ILedgerEntity 的實體: {entityName}");
                }

                var openServiceType = typeof(ILedgerVerificationService<,>);
                var closedServiceType = openServiceType.MakeGenericType(contextType, entityType);
                var service = _serviceProvider.GetService(closedServiceType);

                if (service == null)
                {
                    _logger.LogError("無法從 DI 容器解析服務: {ServiceType}", closedServiceType.FullName);
                    return StatusCode(StatusCodes.Status500InternalServerError, "對應的總帳驗證核心服務未註冊。");
                }

                string? finalDigest = null;
                if (Request.Body.CanSeek || Request.ContentLength > 0)
                {
                    using var reader = new StreamReader(Request.Body);
                    var digestJson = await reader.ReadToEndAsync();
                    finalDigest = string.IsNullOrWhiteSpace(digestJson) ? null : digestJson;
                }

                var method = closedServiceType.GetMethod(nameof(ILedgerVerificationService<DbContext, ILedgerEntity>.VerifyLedgerAsync));
                if (method == null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "無法載入驗證核心方法定義。");
                }

                var invokeResult = method.Invoke(service, [finalDigest]);
                if (invokeResult is not Task<LedgerVerificationResult> ledgerTask)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "無法將驗證執行結果轉換為標準總帳驗證任務。");
                }

                var verificationResult = await ledgerTask;

                // 動態淬取實際的 SQL Server 資料庫名稱
                string actualDbName = contextName;
                if (verificationResult != null && !string.IsNullOrWhiteSpace(verificationResult.ExtractedDigest))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(verificationResult.ExtractedDigest);
                        var root = doc.RootElement;
                        var targetElement = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 ? root[0] : root;

                        if (targetElement.ValueKind == JsonValueKind.Object && targetElement.TryGetProperty("database_name", out var db))
                        {
                            actualDbName = db.GetString() ?? contextName;
                        }
                    }
                    catch
                    {
                        // 降級維持原 Context 名稱
                    }
                }

                var generator = new LedgerPdfReportGenerator();
                var pdfBytes = generator.GenerateReport(verificationResult!, entityName);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "產生的總帳稽核報告內容為空。");
                }

                _logger.LogInformation("成功產生資料庫 {DbName} 的實體 {EntityName} 總帳稽核 PDF 報告。", actualDbName, entityName);

                string cleanTimestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string rawFileName = $"LedgerAudit_{actualDbName}_{entityName}_{cleanTimestamp}.pdf";

                // 使用內建標準的 ContentDisposition 格式化輸出
                var contentDisposition = new System.Net.Mime.ContentDisposition
                {
                    FileName = rawFileName,
                    Inline = false
                };

                // 雙軌塞入標頭，徹底解決各類 API 工具跨域造成的 filename 遺失問題
                Response.Headers.ContentDisposition = contentDisposition.ToString();
                Response.Headers.Append("content-disposition", $"attachment; filename=\"{rawFileName}\"");
                Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition, content-disposition");

                return File(pdfBytes, "application/pdf", rawFileName);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                _logger.LogError(ex.InnerException, "執行總帳驗證下載報告時核心底層拋出異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = ex.InnerException.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "路由 {ContextName}/{EntityName} 總帳驗證報告下載請求處理失敗。", contextName, entityName);
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "通用泛型總帳控制器處理常式錯誤", Detail = ex.Message });
            }
        }
    }
}