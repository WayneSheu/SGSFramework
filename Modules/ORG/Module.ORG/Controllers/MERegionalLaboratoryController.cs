using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Controllers
{
    [ApiController]
    [Route("api/org/laboratories/me")]
    [Authorize]
    [RequireLaboratoryAttribute("ME")] // 限定僅允許 ME 實驗室或其管轄之區域實驗室呼叫
    public sealed class MERegionalLaboratoryController : ControllerBase
    {
        [HttpGet("regional")]
        public async Task<IActionResult> GetMeRegionalLaboratoriesAsync()
        {
            await Task.CompletedTask;
            return Ok(new { success = true, message = "成功通過動態規則引擎驗證，取得 ME 區域實驗室資料。" });
        }
    }
}
