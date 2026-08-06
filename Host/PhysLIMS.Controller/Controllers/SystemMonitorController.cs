using Microsoft.AspNetCore.Mvc;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.ModulePlugin.Systems.Module;
using System.ComponentModel;

namespace SGSFramework.ApiInfrastructure.Controllers
{
    [ApiController]
    [ApiVersion("v1")]
    [Route("api/system/SystemMonitor")]
    [Menu("系統監控", "fa-solid fa-flask", order: 10, parent: null)]
    [RequiresPermission("SYSTEM_MONITIOR_READ")]
    [Description("系統監控")] 
    public class SystemMonitorController : ControllerBase
    {
        private readonly ServiceRegistryMonitor _monitor;

        public SystemMonitorController(ServiceRegistryMonitor monitor) => _monitor = monitor;

        [HttpGet("modules")]
        public IActionResult GetModuleRegistrations()
        {



            return Ok(_monitor.ModuleRegistrations);
        }
    }
}
