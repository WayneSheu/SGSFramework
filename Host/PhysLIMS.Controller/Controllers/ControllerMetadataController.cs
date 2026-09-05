using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;

namespace SGSFramework.ApiInfrastructure.Controllers
{
    /// <summary>
    /// 系統控制器中繼資料管理
    /// </summary>
    [ApiController]
    [Route("api/v1/controller-metadatas")]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [Authorize]
    [ControllerTitle("系統管理", Icon = "fa-solid fa-server", Order = 22, Description = "提供查詢系統註冊之所有 Controller 與 Function 中繼資料清單")]
    [RequiresPermission("SYSTEM.CONTROLLERMETADATA.READ")]
    public sealed class ControllerMetadataController(
        IControllerMetadataService controllerMetadataService,
        ILogger<ControllerMetadataController> logger) : ApiControllerBase
    {
        private readonly IControllerMetadataService _controllerMetadataService = controllerMetadataService ?? throw new ArgumentNullException(nameof(controllerMetadataService));
        private readonly ILogger<ControllerMetadataController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// 取得系統所有控制器中繼資料清單
        /// </summary>
        /// <param name="cancellationToken">異步取消權牌</param>
        /// <returns>控制器中繼資料清單集合</returns>
        [HttpGet]
        [Function("GetAllControllerMetadatas", "控制器中繼資料列表", Icon = "fa-solid fa-list", Order = 1, Description = "取得系統所有 Controller 與 Function 中繼資料清單")]
        [ProducesResponseType(typeof(IEnumerable<ControllerMetadataDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [RequiresPermission("SYSTEM.CONTROLLERMETADATA.GETALL")]
        public async Task<IActionResult> GetAllControllerMetadatas(CancellationToken cancellationToken = default)
        {
            try
            {
                var metadatas = await _controllerMetadataService.GetAllControllerMetadatasAsync(cancellationToken);
                _logger.LogInformation("成功查詢系統控制器中繼資料列表，總筆數: {Count}", metadatas is ICollection<ControllerMetadataDto> coll ? coll.Count : -1);
                return Ok(metadatas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢控制器中繼資料列表時發生未預期異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "伺服器內部錯誤",
                    Detail = "查詢控制器中繼資料列表時發生系統異常，請聯繫系統管理員。",
                    Instance = HttpContext.Request.Path
                });
            }
        }
    }
}
