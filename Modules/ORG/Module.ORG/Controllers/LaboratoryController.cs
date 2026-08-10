using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGS.Modules.ORG.Application.Features.Laboratories.Command;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Application.Features.Laboratories.Query;





//using SES.Controller.Base;
using System.ComponentModel;


namespace SGS.Modules.ORG.Controllers
{

    /// <summary>
    /// 實驗室維護
    /// </summary>
    [ApiController]
    [ApiVersion("v1")]
    [Route("api/org/laboratorys")]
    [Menu("實驗室管理", "fa-solid fa-flask", order: 10, parent: "組織管理")]
    [RequiresPermission("ORG_LAB_READ")]
    [Description("實驗室維護")]
    public class LaboratoryController : ApiControllerBase
    {
    
        private ILogger<LaboratoryController> _logger;
        private IMediator _mediator;
    
        public LaboratoryController(ILogger<LaboratoryController> logger, IMediator mediator)
        {
            _logger = logger ?? throw new ArgumentNullException();
            _mediator = mediator ?? throw new ArgumentNullException();

        }


        // 取得實驗室列表
        [HttpGet("GetLaboratoies")]
        [Menu("取得實驗室清單", "fa-solid fa-list", order: 1, parent: "實驗室管理")] 
        [RequiresPermission("ORG_LAB_READ")]//定義方法層級權限
        [Order(1)]                          // Action 排序
        [Description("取得實驗室清單")]       // Action 描述
        public async Task<IActionResult> GetLaboratoies()
        {
            //var query = new GetLaboratoriesQuery();  
            //var laboraties = await _mediator.Send(query);

            // _logger.LogInformation("Retrieved {Count} laboratories.", laboraties);
            // return Ok(laboraties);

            var query = new GetLaboratoriesQuery();

            // 1. 取得 Controller 視角下的 Expected Handler Type
            var expectedType = typeof(IRequestHandler<GetLaboratoriesQuery, List<LaboratoryDto>>);

            // 2. 測試直接從 HttpContext.RequestServices 解析
            var handler = HttpContext.RequestServices.GetService(expectedType);

            _logger.LogInformation("=== MediatR 診斷資訊 ===");
            _logger.LogInformation("Expected Type: {Type}", expectedType.AssemblyQualifiedName);
            _logger.LogInformation("Query Assembly Path: {Path}", query.GetType().Assembly.Location);
            _logger.LogInformation("Handler Resolved Success: {Success}", handler != null);
            _logger.LogInformation("========================");

            var laboratories = await _mediator.Send(query);
            return Ok(laboratories);
        }

        // 1. 取得單一實驗室基本資訊 (無下階層)
        [HttpGet("GetLaboratory/{id:int}")]
        [Menu("取得特定實驗室資訊", "fa-solid fa-flask-vial", order: 2, parent: "實驗室管理")]
        [RequiresPermission("ORG_LAB_READ")]
        [Order(2)]
        [Description("依據 Id 取得單一實驗室基本資訊")]
        public async Task<IActionResult> GetLaboratory([FromRoute] int id)
        {
            var query = new GetLaboratoryByIdQuery(id);
            var result = await _mediator.Send(query);
            return HandleResult(result);
        }

        // 2. 取得特定實驗室及其完整子樹結構 (包含下階層 Children)
        [HttpGet("GetLaboratoryTree/{id:int}")]
        [Menu("取得特定實驗室子樹", "fa-solid fa-sitemap", order: 3, parent: "實驗室管理")]
        [RequiresPermission("ORG_LAB_READ")]
        [Order(3)]
        [Description("依據 Id 取得特定實驗室及其完整下階層樹狀結構")]
        public async Task<IActionResult> GetLaboratoryTree([FromRoute] int id)
        {
            var query = new GetLaboratoryTreeByIdQuery(id);
            var result = await _mediator.Send(query);
            return HandleResult(result);
        }

        // 
        [HttpPost("CreateLaboratory")]
        [Menu("新增實驗室", "fa-solid fa-list", order: 2, parent: "實驗室管理")] 
        [RequiresPermission("ORG_LAB_CREATE")]
        [Order(2)]                          // Action 排序
        [Description("新增實驗室")]
        public async Task<IActionResult> CreateLaboratory(AddLaboratoryCommand  command)
        {
          
            var result = await _mediator.Send(command);
            if(result.IsSuccess)
            {
                var lab = result.Value;
                //return CreatedAtAction("GetLaboratory", new { id = lab.Id }, lab);
                // 使用 @ 來解構並序列化物件
                _logger.LogInformation("新增實驗室:{@lab}成功.",lab);
                //return Ok(result);
                return HandleResult(result);
            }
            else
            {
                _logger.LogError(@"新增實驗室失敗. 錯誤: {ErrorMessage}", result.Error);
                return BadRequest();
            }
        }

        [HttpPatch("PathLaboratory")]
        [Menu("更新實驗室", "fa-solid fa-list", order: 3, parent: "實驗室管理")]
        [RequiresPermission("ORG_LAB_PATCH")]                         
        [Description("部分更新（Patch）實驗室")]
        public async Task<IActionResult> PathLaboratory(PathLaboratoryCommand command)
        {

            var result = await _mediator.Send(command);
            if (result.IsSuccess)
            {
                _logger.LogInformation("Path 實驗室:{@lab}成功.", result.Value);
            }
            else
            {
                _logger.LogError(@"Path 實驗室失敗. 錯誤: {ErrorMessage}", result.Error);
            }
            return HandleResult(result);
        }

        
        [HttpPut("EditLaboratory")]
        [Menu("編輯實驗室", "fa-solid fa-list", order: 4, parent: "實驗室管理")]
        [RequiresPermission("ORG_LAB_PUT")]                       
        [Description("完整編輯（Put）實驗室")]
        public async Task<IActionResult> EditLaboratory(EditLaboratoryCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsSuccess)
            {
                var lab = result.Value;
                _logger.LogInformation("編輯實驗室:{@lab}成功.", lab);
            }
            else
            {
                _logger.LogError(@"編輯實驗室失敗. 錯誤: {ErrorMessage}", result.Error);
            }
            return HandleResult(result);
        }

        [HttpDelete("DeleteLaboratory")]
        [Menu("刪除實驗室", "fa-solid fa-list", order: 5, parent: "實驗室管理")]
        [RequiresPermission("ORG_LAB_DELETE")]                        
        [Description("刪除實驗室")]
        public async Task<IActionResult> DeleteLaboratory(DeleteLaboratoryCommand command)
        {

            var result = await _mediator.Send(command);
            if (result.IsSuccess) { 
               _logger.LogInformation("Delete laboratory with ID: {Id} successfully.", command.Id);
            }
            else
            {
                _logger.LogError(@"Delete laboratory with ID: {Id} failed. Error: {ErrorMessage}", command.Id, result.Error);
            }

            return HandleResult(result);
        }

    }
}
