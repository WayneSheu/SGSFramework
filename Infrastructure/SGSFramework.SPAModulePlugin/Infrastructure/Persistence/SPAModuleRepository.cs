namespace SGSFramework.SPAModulePlugin.Infrastructure.Persistence;

using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.SPAModulePlugin.Domain.Abstractions;
using SGSFramework.SPAModulePlugin.Domain.Enums;
using SGSFramework.SPAModulePlugin.Domain.Repositories;
using SGSFramework.SPAModulePlugin.Domain.ValueObjects;

public sealed class SPAModuleRepository : ISPAModuleRepository
{
    private readonly IWebHostEnvironment _env;
    private readonly ISPAModuleSecurityVerifier _verifier;
    private readonly ILogger<SPAModuleRepository> _logger;

    public SPAModuleRepository(
        IWebHostEnvironment env,
        ISPAModuleSecurityVerifier verifier,
        ILogger<SPAModuleRepository> logger)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<SPAModuleManifest>> GetValidModulesAsync(SPAFrameworkType frameworkType, CancellationToken cancellationToken = default)
    {
        var validModules = new List<SPAModuleManifest>();
        string webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");

        // 定義各框架動態模組掃描目錄
        string targetDirectory = frameworkType switch
        {
            SPAFrameworkType.Vue3 => Path.Combine(webRoot, "modules"),
            SPAFrameworkType.BlazorWasm => Path.Combine(webRoot, "blazor", "dynamic-modules"),
            _ => throw new ArgumentOutOfRangeException(nameof(frameworkType), frameworkType, null)
        };

        if (!Directory.Exists(targetDirectory))
        {
            _logger.LogWarning("SPA 模組目錄不存在：{Directory}", targetDirectory);
            return validModules.AsReadOnly();
        }

        // 搜尋目錄下所有的 module.manifest.json 設定檔
        var manifestFiles = Directory.GetFiles(targetDirectory, "module.manifest.json", SearchOption.AllDirectories);

        foreach (var manifestPath in manifestFiles)
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
                var manifest = JsonSerializer.Deserialize<SPAModuleManifest>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest is null) continue;

                // 組合 EntryPoint 實體路徑並執行零信任雜湊校驗
                string entryPointPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.EntryPoint);
                if (_verifier.VerifyModuleIntegrity(entryPointPath, manifest.FileHash))
                {
                    validModules.Add(manifest);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析 SPA 模組 Manifest 失敗，路徑：{Path}", manifestPath);
            }
        }

        return validModules.AsReadOnly();
    }
}