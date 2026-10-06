// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Infrastructure/Services/SPAModuleDiscoveryService.cs
namespace SGSFramework.SPAModulePlugin.Infrastructure.Services;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.SPAModulePlugin.Application.Abstractions;
using SGSFramework.SPAModulePlugin.Domain.Enums;
using SGSFramework.SPAModulePlugin.Domain.ValueObjects;
using System.Text.Json;

/// <summary>
/// SPA 模組掃描與授權驗證服務實作
/// </summary>
public sealed class SPAModuleDiscoveryService : ISPAModuleDiscoveryService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SPAModuleDiscoveryService> _logger;

    public SPAModuleDiscoveryService(IWebHostEnvironment env, ILogger<SPAModuleDiscoveryService> logger)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<SPAModuleManifest>> GetAuthorizedModulesAsync(
        Guid userId,
        SPAFrameworkType frameworkType,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("使用者識別碼不可為空 Guid。", nameof(userId));
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var baseDir = frameworkType switch
        {
            SPAFrameworkType.Vue3 => Path.Combine(webRoot, "modules"),
            SPAFrameworkType.BlazorWasm => Path.Combine(webRoot, "blazor", "dynamic-modules"),
            _ => throw new ArgumentOutOfRangeException(nameof(frameworkType), frameworkType, "不支援的前端框架類型。")
        };

        var manifests = new List<SPAModuleManifest>();

        if (!Directory.Exists(baseDir))
        {
            _logger.LogWarning("SPA 模組根目錄不存在：{DirectoryPath}", baseDir);
            return manifests.AsReadOnly();
        }

        try
        {
            var moduleDirectories = Directory.GetDirectories(baseDir);

            foreach (var moduleDir in moduleDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var manifestFilePath = Path.Combine(moduleDir, "manifest.json");
                if (!File.Exists(manifestFilePath))
                {
                    continue;
                }

                try
                {
                    using var stream = File.OpenRead(manifestFilePath);
                    var manifest = await JsonSerializer.DeserializeAsync<SPAModuleManifest>(
                        stream,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                        cancellationToken).ConfigureAwait(false);

                    if (manifest != null)
                    {
                        // TODO: 整合 RBAC 權限比對 (例如: 使用者角色或 Bitmask 權限鏈驗證)
                        manifests.Add(manifest);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析 SPA 模組 Manifest 檔案失敗：{FilePath}", manifestFilePath);
                }
            }

            return manifests.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "讀取 SPA 模組目錄資訊時發生例外：{DirectoryPath}", baseDir);
            throw;
        }
    }
}