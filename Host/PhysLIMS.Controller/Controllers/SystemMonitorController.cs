using Microsoft.AspNetCore.Mvc;
using SGSFramework.ModulePlugin.Systems.Module;

namespace SGSFramework.ApiInfrastructure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
