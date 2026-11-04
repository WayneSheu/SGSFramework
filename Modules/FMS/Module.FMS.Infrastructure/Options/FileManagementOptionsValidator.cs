using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Infrastructure.Options;

/// <summary>
/// 檔案管理組態的跨欄位驗證（啟動時 ValidateOnStart）：儲存根目錄須為絕對路徑且不在應用程式目錄 / wwwroot 內、
/// 分類引用的儲存目標須已設定、縮圖尺寸白名單格式、快取水位。FileManagement:Enabled = false 時略過。
/// </summary>
public sealed class FileManagementOptionsValidator(IHostEnvironment environment) : IValidateOptions<FileManagementOptions>
{
    public ValidateOptionsResult Validate(string? name, FileManagementOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled) return ValidateOptionsResult.Success;

        var errors = new[]
            {
                ValidateTargets(options, environment), ValidateCategories(options),
                ValidateImageCache(options), ValidateSizes(options.Image)
            }
            .Where(e => e is not null)
            .Cast<string>()
            .ToList();

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    internal static string? ValidateTargets(FileManagementOptions o, IHostEnvironment env)
    {
        if (o.Targets.Count == 0) return "FileManagement:Targets 至少需設定一個儲存目標。";

        var forbiddenRoots = new[] { env.ContentRootPath, AppContext.BaseDirectory }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p)) + Path.DirectorySeparatorChar)
            .ToArray();

        foreach (var (target, settings) in o.Targets)
        {
            if (settings.Provider != StorageProviderKind.FileSystem) continue;

            if (string.IsNullOrWhiteSpace(settings.RootPath) || !Path.IsPathFullyQualified(settings.RootPath))
                return $"FileManagement:Targets:{target}:RootPath 必須為絕對路徑或 UNC（建議以環境變數 FileManagement__Targets__{target}__RootPath 注入）。";

            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settings.RootPath)) + Path.DirectorySeparatorChar;
            if (forbiddenRoots.Any(f => root.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
                return $"FileManagement:Targets:{target}:RootPath 不可位於應用程式目錄或 wwwroot 內（{settings.RootPath}）。";
        }

        return null;
    }

    internal static string? ValidateCategories(FileManagementOptions o)
    {
        if (o.Categories.Count == 0)
        {
            if (!o.DefaultCategory.Equals(FileCategoryOptions.GeneralCategory, StringComparison.OrdinalIgnoreCase))
                return $"FileManagement:DefaultCategory「{o.DefaultCategory}」未定義於 Categories。";
            return o.Targets.ContainsKey(StorageTarget.LocalPrivate) ? null : "內建 General 分類需設定 FileManagement:Targets:LocalPrivate。";
        }

        if (!o.Categories.ContainsKey(o.DefaultCategory))
            return $"FileManagement:DefaultCategory「{o.DefaultCategory}」未定義於 Categories。";

        foreach (var (code, category) in o.Categories)
        {
            if (!o.Targets.ContainsKey(category.Target))
                return $"FileManagement:Categories:{code} 的儲存目標 {category.Target} 未設定於 Targets。";
            if (category.MaxFileSizeBytes > o.MaxFileSizeBytes)
                return $"FileManagement:Categories:{code}:MaxFileSizeBytes 不可超過全域上限 MaxFileSizeBytes。";
            if (category.AllowedExtensions.Any(e => e.Trim().TrimStart('.').ToLowerInvariant() is "svg" or "html" or "htm" or "exe" or "dll" or "js" or "bat" or "cmd" or "ps1"))
                return $"FileManagement:Categories:{code}:AllowedExtensions 含有不允許的高風險副檔名。";
        }

        return null;
    }

    internal static string? ValidateImageCache(FileManagementOptions o)
    {
        var cache = o.ImageCache;
        if (!cache.Enabled) return null;
        if (string.IsNullOrWhiteSpace(cache.RootPath) || !Path.IsPathFullyQualified(cache.RootPath))
            return "FileManagement:ImageCache:RootPath 必須為絕對路徑（建議為本機 SSD）。";
        return cache.LowWatermarkBytes < cache.HighWatermarkBytes ? null : "FileManagement:ImageCache:LowWatermarkBytes 必須小於 HighWatermarkBytes。";
    }

    internal static string? ValidateSizes(ImageProcessingOptions image)
    {
        foreach (var size in image.AllowedSizes)
        {
            var parts = size.Trim().Split('x', 'X');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var w) || !int.TryParse(parts[1], out var h) ||
                w < 0 || h < 0 || (w == 0 && h == 0) || w > image.MaxDimension || h > image.MaxDimension)
            {
                return $"FileManagement:Image:AllowedSizes「{size}」格式不正確。";
            }
        }

        return null;
    }
}
