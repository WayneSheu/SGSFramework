using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.ReportEngine.Abstractions;
using SGSFramework.ReportEngine.Security;
using System.Security.Claims;

namespace SGSFramework.ReportEngine.Controllers
{
    /// <summary>
    /// 報表控制器的抽象基底類別，整合權限控管、快取讀取與 PDF 檔案回傳功能
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/reports")]
    public abstract class ReportControllerBase<TReportData> : ControllerBase where TReportData : class, IReportData
    {
        protected readonly IReportAuthorizationService _authorizationService;
        protected readonly IReportCacheService _cacheService;

        protected ReportControllerBase(
            IReportAuthorizationService authorizationService,
            IReportCacheService cacheService)
        {
            _authorizationService = authorizationService;
            _cacheService = cacheService;
        }

        /// <summary>
        /// 執行權限驗證並透過快取或指定產生器輸出 PDF 檔案下載
        /// </summary>
        /// <param name="reportTypeCode">報表類別代碼（用於權限驗證）</param>
        /// <param name="cacheKey">報表資料快取 Key</param>
        /// <param name="fileName">下載的 PDF 檔案名稱</param>
        /// <param name="generatorFactory">當快取不存在時的報表產生器原廠委派</param>
        protected async Task<IActionResult> CreateReportPdfResultAsync(
            string reportTypeCode,
            string cacheKey,
            string fileName,
            Func<TReportData, byte[]> generatorFactory)
        {
            // 1. 驗證報表類別權限
            var hasPermission = await _authorizationService.AuthorizeAsync(User, reportTypeCode);
            if (!hasPermission)
            {
                return Forbid();
            }

            // 2. 從快取取得報表資料
            var reportData = await _cacheService.GetCacheAsync<TReportData>(cacheKey);
            if (reportData == null)
            {
                return NotFound(new { message = "找不到對應的報表資料或快取已過期，請重新查詢。" });
            }

            // 3. 透過產生器建立 PDF 二進位資料
            byte[] pdfBytes;
            try
            {
                pdfBytes = generatorFactory(reportData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "產生報表 PDF 時發生錯誤", details = ex.Message });
            }

            // 4. 回傳檔案下載
            return File(pdfBytes, "application/pdf", $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
        }
    }
}