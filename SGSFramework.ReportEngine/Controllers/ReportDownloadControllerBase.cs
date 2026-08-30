using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.ReportEngine.Abstractions;
using SGSFramework.ReportEngine.Security;

namespace SGSFramework.ReportEngine.Controllers
{
    /// <summary>
    /// 專門提供報表下載與非同步任務串接的通用 Controller 基底類別
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/reports")]
    public abstract class ReportDownloadControllerBase<TReportData> : ControllerBase where TReportData : class, IReportData
    {
        protected readonly IReportAuthorizationService _authorizationService;
        protected readonly IReportCacheService _cacheService;

        protected ReportDownloadControllerBase(
            IReportAuthorizationService authorizationService,
            IReportCacheService cacheService)
        {
            _authorizationService = authorizationService;
            _cacheService = cacheService;
        }

        /// <summary>
        /// 驗證權限並從快取取得資料後輸出 PDF 檔案串流下載
        /// </summary>
        /// <param name="reportTypeCode">報表類別代碼（供權限控管驗證）</param>
        /// <param name="cacheKey">快取鍵值</param>
        /// <param name="fileDownloadName">下載時顯示的檔案名稱前綴</param>
        /// <param name="reportGenerator">報表產生邏輯委派（帶入資料回傳 PDF bytes）</param>
        protected async Task<IActionResult> ExecuteDownloadPdfAsync(
            string reportTypeCode,
            string cacheKey,
            string fileDownloadName,
            Func<TReportData, byte[]> reportGenerator)
        {
            // 1. 執行報表類別權限控管驗證
            var hasPermission = await _authorizationService.AuthorizeAsync(User, reportTypeCode);
            if (!hasPermission)
            {
                return Forbid();
            }

            // 2. 檢查快取資料是否存在
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return BadRequest(new { message = "無效的快取識別碼。" });
            }

            var reportData = await _cacheService.GetCacheAsync<TReportData>(cacheKey);
            if (reportData == null)
            {
                return NotFound(new { message = "找不到指定的報表資料，或快取已過期失效，請重新執行查詢。" });
            }

            // 3. 透過 QuestPDF 產生器產出 PDF 二進位資料
            byte[] pdfBytes;
            try
            {
                pdfBytes = reportGenerator(reportData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "產生 PDF 報表過程發生未預期的錯誤。", error = ex.Message });
            }

            // 4. 回傳實體檔案下載
            var finalFileName = $"{fileDownloadName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(pdfBytes, "application/pdf", finalFileName);
        }
    }
}