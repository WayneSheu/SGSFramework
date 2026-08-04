using Microsoft.AspNetCore.Mvc;
using Serilog.Core;
using Serilog.Events;

namespace SGSFramework.ApiInfrastructure.Controllers
{
    [ApiController]
    [Route("api/system/logs")]
    public class LogSettingsController : ControllerBase
    {
        private readonly LoggingLevelSwitch _levelSwitch;

        public LogSettingsController(LoggingLevelSwitch levelSwitch)
        {
            _levelSwitch = levelSwitch;
        }

        [HttpGet("current-level")]
        public IActionResult GetLevel()
        {
            var level = _levelSwitch.MinimumLevel.ToString();
            return Ok($"Log  current-level {level}");
        }


        [HttpPost("set-level")]
        public IActionResult SetLevel(LogEventLevel level)
        {
            // 這裡一修改，全系統日誌輸出立即改變，無需重啟
            _levelSwitch.MinimumLevel = level;
            return Ok($"Log level changed to {level}");
        }



    }
}
