// File: src/Host/SES.API/Controllers/ModuleManagementController.cs
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.DTOs;
using SGSFramework.ModulePlugin.Systems.Module.Loaders;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace SGSFramework.ModulePlugin.Controllers.ModuleManagements
{
    [ApiController]
    [Route("api/system/modules")]
    [Menu("模組管理", "fa-solid fa-flask", order: 10, parent: null)]
    [RequiresPermission("MODULEMANAGEMENT_READ")]
    [Description("商業模組維護")]
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
        [HttpGet("details")]
        [Menu("查詢已載入模組詳情", "fa-solid fa-list-check", order: 1, parent: "模組管理")]
        [RequiresPermission("MODULEMANAGEMENT_GETACTIVEMODULESDETAILS")]
        [Order(1)]
        [Description("查詢目前所有已載入掛載的商務模組完整資訊")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ModuleDetailResponse>>> GetActiveModulesDetails()
        {
            try
            {
                var loadedModuleNames = _moduleRegistry.GetLoadedModules()?.ToList() ?? new List<string>();
                var allDbModules = await _moduleRepo.GetAllModulesAsync();

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
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"查詢已載入模組詳情時發生系統異常：{ex.Message}" });
            }
        }

        /// <summary>
        /// 上傳並儲存新的模組檔案（支援單一 DLL 或多檔案批次上傳，自動解除檔案鎖定、更新並同步註冊控制器元資料）
        /// </summary>
        [HttpPost("upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Menu("上傳模組", "fa-solid fa-upload", order: 3, parent: "模組管理")]
        [RequiresPermission("MODULEMANAGEMENT_UPLOADMODULE")]
        [Order(4)]
        [Description("上傳並儲存新的模組檔案")]
        public async Task<IActionResult> UploadModule([FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { message = "上傳的檔案不可為空。" });
            }

            var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
            if (!Directory.Exists(pluginsDirectory))
            {
                Directory.CreateDirectory(pluginsDirectory);
            }

            var uploadedResults = new List<object>();
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
                            await file.CopyToAsync(stream);
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
                        await file.CopyToAsync(stream);
                    }

                    uploadedResults.Add(new { fileName = safeFileName, size = file.Length });
                }

                if (string.IsNullOrEmpty(detectedModuleName))
                {
                    return BadRequest(new { message = "無法識別上傳的主模組 DLL 檔案名稱。" });
                }

                try
                {
                    _moduleRegistry.UnloadModule(detectedModuleName);
                }
                catch
                {
                }

                var mainDllFileName = $"{detectedModuleName}.dll";
                var mainDllFullPath = Path.Combine(pluginsDirectory, mainDllFileName);

                if (System.IO.File.Exists(mainDllFullPath))
                {
                    var assembly = Assembly.LoadFrom(mainDllFullPath);

                    // 建立獨立的 Scope 以便正確解析 IDynamicControllerRepository 與 IModuleRepository 進行資料庫寫入
                    using var scope = _serviceProvider.CreateScope();
                    await ModuleLoaderExtensions.RegisterModuleToDbAsync(assembly, detectedModuleName, mainDllFullPath, scope.ServiceProvider);
                }

                return Ok(new
                {
                    success = true,
                    message = $"模組 [{detectedModuleName}] 相關檔案上傳成功，已完成元資料與控制器同步。",
                    moduleName = detectedModuleName,
                    version = detectedVersion,
                    files = uploadedResults
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"上傳模組時發生系統異常：{ex.Message}" });
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
        /// 切換模組的啟用狀態
        /// </summary>
        [HttpPost("{moduleName}/toggle")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Menu("切換模組狀態", "fa-solid fa-toggle-on", order: 3, parent: "模組管理")]
        [RequiresPermission("MODULEMANAGEMENT_TOGGLESTATUS")]
        [Order(2)]
        [Description("切換模組的啟用狀態")]
        public async Task<IActionResult> ToggleStatus(string moduleName, [FromBody] ToggleStatusRequest request)
        {
            var module = await _moduleRepo.GetModuleByNameAsync(moduleName);

            if (module == null)
            {
                return BadRequest(new { message = $"無法啟用模組 {moduleName}：資料庫中找不到對應的模組元資料紀錄。" });
            }

            if (request.IsActive)
            {
                if (string.IsNullOrEmpty(module.AssemblyPath) || !System.IO.File.Exists(module.AssemblyPath))
                {
                    var fallbackPath = Path.Combine(AppContext.BaseDirectory, "plugins", $"{moduleName}.dll");
                    if (System.IO.File.Exists(fallbackPath))
                    {
                        module.AssemblyPath = fallbackPath;
                        await _moduleRepo.UpsertAsync(module);
                    }
                    else
                    {
                        return BadRequest(new { message = $"無法啟用模組 {moduleName}：找不到對應的 DLL 檔案（路徑：{module.AssemblyPath}）。" });
                    }
                }
            }

            await _moduleRepo.ToggleModuleStatusAsync(moduleName, request.IsActive);
            return Ok(new { success = true, module = moduleName, active = request.IsActive });
        }

        /// <summary>
        /// 線上動態卸載並同步清除指定模組與其對應的控制器元資料
        /// </summary>
        [HttpDelete("{name}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Menu("卸載模組", "fa-solid fa-trash", order: 4, parent: "模組管理")]
        [RequiresPermission("MODULEMANAGEMENT_REMOVEMODULE")]
        [Order(3)]
        [Description("線上動態卸載指定模組並同步清除資料庫中的控制器與模組元資料")]
        public async Task<IActionResult> RemoveModule(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            try
            {
                _moduleRegistry.UnloadModule(name);
                await _moduleRepo.RemoveModuleAsync(name);

                return Ok(new { message = $"模組 [{name}] 已成功從記憶體卸載，並已同步清除資料庫中的元資料。" });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"卸載並同步模組時發生系統異常：{ex.Message}" });
            }
        }
    }
}