using Microsoft.AspNetCore.Mvc;
using Serilog.Core;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Attributes;
using System.ComponentModel;

namespace SGSFramework.SystemLog.Controllers
{
    [ApiController]
    [ApiVersion("v1")]
    [Route("api/system/LogManager")]
    [Menu("系統日誌管理", "fa-solid fa-flask", order: 10, parent: null)]
    [RequiresPermission("SYSTEM_LOGMANAGER_READ")]
    [Description("系統日誌管理")]
    public class LogManagerController : ControllerBase
    {
        private readonly LoggingLevelSwitch _levelSwitch;

        public LogManagerController(LoggingLevelSwitch levelSwitch)
        {
            _levelSwitch = levelSwitch;
        }

        [HttpGet("current-level")]
        [Menu("取得日誌層級", "fa-solid fa-flask", order: 10, parent: "系統日誌管理")]
        [RequiresPermission("SYSTEM_LOGMANAGER_GETLEVEL")]
        [Description("取得系統日誌層級")]
        public IActionResult GetLevel()
        {
            var level = _levelSwitch.MinimumLevel.ToString();
            return Ok($"Log  current-level {level}");
        }


        [HttpPost("set-level")]
        [Menu("設定日誌層級", "fa-solid fa-flask", order: 10, parent: "系統日誌管理")]
        [RequiresPermission("SYSTEM_LOGMANAGER_SETLEVEL")]
        [Description("設定系統日誌層級")]
        public IActionResult SetLevel(LogEventLevel level)
        {
            // 這裡一修改，全系統日誌輸出立即改變，無需重啟
            _levelSwitch.MinimumLevel = level;
            return Ok($"Log level changed to {level}");
        }


    }
}
