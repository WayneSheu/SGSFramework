using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog.Core;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Attributes;

namespace SGSFramework.SystemLog.Controllers;

/// <summary>
/// 系統日誌動態切換與檔案查詢控制器
/// </summary>
[ApiController]
[ApiVersion("v1")]
[Route("api/system/log-manager")]
[ControllerTitle("系統日誌管理", Icon = "fa-solid fa-receipt", Order = 90, Description = "動態調整 Serilog 紀錄層級與線上檢視系統 Log 檔案內容")]
[RequiresPermission("SYSTEM.LOGMANAGER.READ")]
public class LogManagerController : ControllerBase
{
    private readonly LoggingLevelSwitch _levelSwitch;

    public LogManagerController(LoggingLevelSwitch levelSwitch)
    {
        _levelSwitch = levelSwitch ?? throw new ArgumentNullException(nameof(levelSwitch));
    }

    /// <summary>
    /// 取得當前全系統的 Serilog 最小日誌輸出層級
    /// </summary>
    [HttpGet("current-level")]
    [Function("GetLevel", "取得日誌層級", Icon = "fa-solid fa-gauge-high", Order = 1, Description = "查詢系統當前動態生效中的日誌輸出層級")]
    [RequiresPermission("SYSTEM.LOGMANAGER.GETLEVEL")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetLevel()
    {
        var level = _levelSwitch.MinimumLevel.ToString();
        return Ok(new
        {
            MinimumLevel = level,
            CheckedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 動態設定全系統日誌輸出層級 (立即生效，無需重啟服務)
    /// </summary>
    [HttpPost("set-level")]
    [Function("SetLevel", "設定日誌層級", Icon = "fa-solid fa-sliders", Order = 2, Description = "即時修改 Serilog 最小日誌輸出層級 (Verbose, Debug, Information, Warning, Error, Fatal)")]
    [RequiresPermission("SYSTEM.LOGMANAGER.SETLEVEL")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult SetLevel([FromBody] LogEventLevel level)
    {
        if (!Enum.IsDefined(typeof(LogEventLevel), level))
        {
            return BadRequest("傳入的日誌層級無效。");
        }

        _levelSwitch.MinimumLevel = level;
        return Ok(new
        {
            Message = $"系統日誌層級已成功變更為: {level}",
            NewLevel = level.ToString(),
            UpdatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 查詢伺服器實體日誌目錄中的檔案清單
    /// </summary>
    [HttpGet("files")]
    [Function("GetLogFiles", "取得日誌檔案清單", Icon = "fa-solid fa-folder-open", Order = 3, Description = "列出 logs 目錄下所有的實體日誌檔案名稱與檔案大小")]
    [RequiresPermission("SYSTEM.LOGMANAGER.GETLOGFILES")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult GetLogFiles()
    {
        try
        {
            var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDirectory))
            {
                return NotFound("系統尚未建立 logs 目錄或目前無可用的日誌檔案。");
            }

            var directoryInfo = new DirectoryInfo(logDirectory);
            var fileList = directoryInfo.GetFiles("*.log", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => new
                {
                    FileName = f.Name,
                    SizeBytes = f.Length,
                    SizeFormatted = $"{Math.Round((double)f.Length / 1024, 2)} KB",
                    LastModifiedUtc = f.LastWriteTimeUtc
                })
                .ToList();

            return Ok(fileList);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "讀取日誌目錄時發生錯誤。", Detail = ex.Message });
        }
    }

    /// <summary>
    /// 讀取特定日誌檔案之內文 (支援讀取最後 N 列)
    /// </summary>
    [HttpGet("files/{fileName}/content")]
    [Function("GetLogContent", "檢視日誌檔內容", Icon = "fa-solid fa-file-code", Order = 4, Description = "檢視指定的日誌檔案內文，可透過 tailLines 參數指定讀取最後幾行內容")]
    [RequiresPermission("SYSTEM.LOGMANAGER.GETLOGCONTENT")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLogContent([FromRoute] string fileName, [FromQuery] int tailLines = 100)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || Path.GetExtension(fileName) != ".log")
        {
            return BadRequest("非法的日誌檔案名稱。");
        }

        if (tailLines <= 0)
        {
            tailLines = 100;
        }

        try
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "logs", fileName);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"找不到指定的日誌檔案: {fileName}");
            }

            var lines = new List<string>();

            // 使用 FileShare.ReadWrite 避免 Serilog 在寫入日誌時產生的 File Lock 競爭
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lines.Add(line);
                }
            }

            var resultLines = lines.TakeLast(tailLines).ToList();

            return Ok(new
            {
                FileName = fileName,
                TotalLines = lines.Count,
                ReturnedLines = resultLines.Count,
                Content = resultLines
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = $"讀取日誌檔案 {fileName} 時發生錯誤。", Detail = ex.Message });
        }
    }
}