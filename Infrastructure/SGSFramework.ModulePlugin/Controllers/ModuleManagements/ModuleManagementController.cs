using System.ComponentModel;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.DTOs;
using SGSFramework.ModulePlugin.Systems.Module.Loaders;

namespace SGSFramework.ModulePlugin.Controllers.v1;

/// <summary>
/// 商業模組維護與動態載入管理控制器
/// </summary>
[ApiController]
[Route("api/v1/system/modules")]
[Produces("application/json")]
[Menu("商業模組管理", "fa-solid fa-flask", order: 10, parent: null)]
[RequiresPermission("MODULEMANAGEMENT_READ")]
[Description("商業模組維護與動態載入管理")]
[Order(10)]
public class ModuleManagementController : ApiControllerBase
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IModuleRepository _moduleRepo;
    private readonly IServiceProvider _serviceProvider;

    public ModuleManagementController(
        IModuleRegistry moduleRegistry,
        IModuleRepository moduleRepo,
        IServiceProvider serviceProvider)
    {
        _moduleRegistry = moduleRegistry ?? throw new ArgumentNullException(nameof(moduleRegistry));
        _moduleRepo = moduleRepo ?? throw new ArgumentNullException(nameof(moduleRepo));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// 查詢目前所有已載入掛載的商務模組完整資訊
    /// </summary>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>模組詳細資訊列表</returns>
    [HttpGet]
    [Menu("查詢已載入模組詳情", "fa-solid fa-list-check", order: 1, parent: "商業模組管理")]
    [RequiresPermission("MODULEMANAGEMENT_GETACTIVEMODULESDETAILS")]
    [Order(1)]
    [Description("查詢目前所有已載入掛載的商務模組完整資訊")]
    [ProducesResponseType(typeof(IEnumerable<ModuleDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<ModuleDetailResponse>>> GetActiveModulesDetailsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var loadedModuleNames = _moduleRegistry.GetLoadedModules()?.ToList() ?? new List<string>();
            var allDbModules = await _moduleRepo.GetAllModulesAsync(cancellationToken);

            var activeModuleDetails = allDbModules
                .Where(m => loadedModuleNames.Contains(m.ModuleName, StringComparer.OrdinalIgnoreCase) || m.IsActive)
                .Select(m => new ModuleDetailResponse
                {
                    ModuleName = m.ModuleName,
                    Version = m.Version,
                    AssemblyPath = m.AssemblyPath,
                    IsActive = m.IsActive,
                    LastLoadedAt = m.LastLoadedAt,
                    Checksum = m.Checksum
                })
                .ToList();

            return Ok(activeModuleDetails);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "系統內部錯誤",
                Detail = $"查詢已載入模組詳情時發生系統異常：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 上傳並儲存新的模組檔案
    /// </summary>
    /// <remarks>支援單一 DLL 或多檔案批次上傳，自動解除檔案鎖定、更新並同步註冊控制器元資料。</remarks>
    /// <param name="files">上傳的模組相關檔案列表</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>模組上傳結果資訊</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Menu("上傳模組", "fa-solid fa-upload", order: 3, parent: "商業模組管理")]
    [RequiresPermission("MODULEMANAGEMENT_UPLOADMODULE")]
    [Order(4)]
    [Description("上傳並儲存新的模組檔案")]
    [ProducesResponseType(typeof(ModuleUploadResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadModuleAsync([FromForm] List<IFormFile> files, CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "請求參數無效",
                Detail = "上傳的檔案不可為空。",
                Instance = HttpContext.Request.Path
            });
        }

        var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginsDirectory))
        {
            Directory.CreateDirectory(pluginsDirectory);
        }

        var uploadedFilesResult = new List<UploadedFileInfoResponseDto>();
        string? mainDllTempPath = null;
        string? detectedModuleName = null;
        string? detectedVersion = "1.0.0.0";

        try
        {
            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var safeFileName = Path.GetFileName(file.FileName);
                var filePath = Path.Combine(pluginsDirectory, safeFileName);

                if (safeFileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                    !safeFileName.Contains(".Application.dll", StringComparison.OrdinalIgnoreCase) &&
                    !safeFileName.Contains(".Infrastructure.dll", StringComparison.OrdinalIgnoreCase))
                {
                    mainDllTempPath = Path.GetTempFileName();
                    using (var stream = new FileStream(mainDllTempPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream, cancellationToken);
                    }

                    try
                    {
                        var assemblyName = AssemblyName.GetAssemblyName(mainDllTempPath);
                        detectedModuleName = assemblyName.Name ?? Path.GetFileNameWithoutExtension(safeFileName);
                        detectedVersion = assemblyName.Version?.ToString() ?? "1.0.0.0";
                    }
                    catch
                    {
                        detectedModuleName = Path.GetFileNameWithoutExtension(safeFileName);
                    }
                }

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                uploadedFilesResult.Add(new UploadedFileInfoResponseDto
                {
                    FileName = safeFileName,
                    Size = file.Length
                });
            }

            if (string.IsNullOrEmpty(detectedModuleName))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "檔案格式不正確",
                    Detail = "無法識別上傳的主模組 DLL 檔案名稱。",
                    Instance = HttpContext.Request.Path
                });
            }

            try
            {
                _moduleRegistry.UnloadModule(detectedModuleName);
            }
            catch
            {
                // 吸納卸載舊模組時可能拋出的非關鍵例外
            }

            var mainDllFileName = $"{detectedModuleName}.dll";
            var mainDllFullPath = Path.Combine(pluginsDirectory, mainDllFileName);

            if (System.IO.File.Exists(mainDllFullPath))
            {
                var assembly = Assembly.LoadFrom(mainDllFullPath);
                using var scope = _serviceProvider.CreateScope();
                await ModuleLoaderExtensions.RegisterModuleToDbAsync(assembly, detectedModuleName, mainDllFullPath, scope.ServiceProvider);
            }

            var response = new ModuleUploadResponseDto
            {
                Success = true,
                Message = $"模組 [{detectedModuleName}] 相關檔案上傳成功，已完成元資料與控制器同步。",
                ModuleName = detectedModuleName,
                Version = detectedVersion,
                Files = uploadedFilesResult
            };

            return CreatedAtAction(nameof(GetActiveModulesDetailsAsync), new { moduleName = detectedModuleName }, response);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "檔案處理過程發生錯誤",
                Detail = $"上傳模組時發生系統異常：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
        finally
        {
            if (!string.IsNullOrEmpty(mainDllTempPath) && System.IO.File.Exists(mainDllTempPath))
            {
                try { System.IO.File.Delete(mainDllTempPath); } catch { }
            }
        }
    }

    /// <summary>
    /// 切換或更新指定模組的啟用狀態
    /// </summary>
    /// <param name="moduleName">模組名稱</param>
    /// <param name="request">啟用狀態變更請求</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>切換後狀態</returns>
    [HttpPatch("{moduleName}/status")]
    [Menu("切換模組狀態", "fa-solid fa-toggle-on", order: 3, parent: "商業模組管理")]
    [RequiresPermission("MODULEMANAGEMENT_TOGGLESTATUS")]
    [Order(2)]
    [Description("切換或更新指定模組的啟用狀態")]
    [ProducesResponseType(typeof(ToggleStatusResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleStatusAsync(
        string moduleName,
        [FromBody] ToggleStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(request);

        var module = await _moduleRepo.GetModuleByNameAsync(moduleName, cancellationToken);

        if (module == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "找不到資源",
                Detail = $"無法啟用模組 [{moduleName}]：資料庫中找不到對應的模組元資料紀錄。",
                Instance = HttpContext.Request.Path
            });
        }

        if (request.IsActive)
        {
            if (string.IsNullOrEmpty(module.AssemblyPath) || !System.IO.File.Exists(module.AssemblyPath))
            {
                var fallbackPath = Path.Combine(AppContext.BaseDirectory, "plugins", $"{moduleName}.dll");
                if (System.IO.File.Exists(fallbackPath))
                {
                    module.AssemblyPath = fallbackPath;
                    await _moduleRepo.UpsertAsync(module, cancellationToken);
                }
                else
                {
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "啟用模組失敗",
                        Detail = $"無法啟用模組 [{moduleName}]：找不到對應的 DLL 檔案（路徑：{module.AssemblyPath}）。",
                        Instance = HttpContext.Request.Path
                    });
                }
            }
        }

        await _moduleRepo.ToggleModuleStatusAsync(moduleName, request.IsActive, cancellationToken);

        return Ok(new ToggleStatusResponseDto
        {
            Success = true,
            ModuleName = moduleName,
            IsActive = request.IsActive
        });
    }

    /// <summary>
    /// 線上動態卸載並同步清除指定模組與其對應的控制器元資料
    /// </summary>
    /// <param name="moduleName">欲卸載的模組名稱</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>操作結果訊息</returns>
    [HttpDelete("{moduleName}")]
    [Menu("卸載模組", "fa-solid fa-trash", order: 4, parent: "商業模組管理")]
    [RequiresPermission("MODULEMANAGEMENT_REMOVEMODULE")]
    [Order(3)]
    [Description("線上動態卸載指定模組並同步清除資料庫中的控制器與模組元資料")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveModuleAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        try
        {
            _moduleRegistry.UnloadModule(moduleName);
            await _moduleRepo.RemoveModuleAsync(moduleName, cancellationToken);

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "卸載模組失敗",
                Detail = $"卸載並同步模組 [{moduleName}] 時發生系統異常：{ex.Message}",
                Instance = HttpContext.Request.Path
            });
        }
    }
}