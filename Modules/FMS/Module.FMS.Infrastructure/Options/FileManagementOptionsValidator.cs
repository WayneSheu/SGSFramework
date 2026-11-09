using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.FMS.Infrastructure.Options;

/// <summary>
/// 檔案管理組態的跨欄位驗證（啟動時 ValidateOnStart）：各儲存驅動的必要設定（檔案系統根目錄不得位於應用程式目錄 / wwwroot、
/// S3 金鑰、Azure 連線字串）、分類引用的儲存目標、巢狀設定範圍、縮圖尺寸白名單格式。FileManagement:Enabled = false 時略過。
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
                ValidateNested(options), ValidateSizes(options.Image)
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
            var error = settings.Provider switch
            {
                StorageProviderKind.FileSystem => ValidateFileSystem(target, settings, forbiddenRoots),
                StorageProviderKind.S3 => ValidateS3(target, settings.S3),
                StorageProviderKind.AzureBlob => string.IsNullOrWhiteSpace(settings.AzureBlob.ConnectionString)
                    ? $"FileManagement:Targets:{target}:AzureBlob:ConnectionString 未設定（請以環境變數 FileManagement__Targets__{target}__AzureBlob__ConnectionString 注入）。"
                    : null,
                _ => $"FileManagement:Targets:{target}:Provider 不支援的儲存體種類 {settings.Provider}。"
            };
            if (error is not null) return error;
        }

        return null;
    }

    private static string? ValidateFileSystem(StorageTarget target, StorageTargetOptions settings, string[] forbiddenRoots)
    {
        if (string.IsNullOrWhiteSpace(settings.RootPath) || !Path.IsPathFullyQualified(settings.RootPath))
            return $"FileManagement:Targets:{target}:RootPath 必須為絕對路徑或 UNC（建議以環境變數 FileManagement__Targets__{target}__RootPath 注入）。";

        // 無狀態原則：嚴禁寫入 Web 伺服器的應用程式目錄 / wwwroot
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settings.RootPath)) + Path.DirectorySeparatorChar;
        return forbiddenRoots.Any(f => root.StartsWith(f, StringComparison.OrdinalIgnoreCase))
            ? $"FileManagement:Targets:{target}:RootPath 不可位於應用程式目錄或 wwwroot 內（{settings.RootPath}）。"
            : null;
    }

    private static string? ValidateS3(StorageTarget target, S3StorageOptions s3)
    {
        if (string.IsNullOrWhiteSpace(s3.AccessKey) || string.IsNullOrWhiteSpace(s3.SecretKey))
            return $"FileManagement:Targets:{target}:S3 需設定 AccessKey 與 SecretKey（請以環境變數 FileManagement__Targets__{target}__S3__SecretKey 注入）。";
        if (!string.IsNullOrWhiteSpace(s3.ServiceUrl) && !Uri.TryCreate(s3.ServiceUrl, UriKind.Absolute, out _))
            return $"FileManagement:Targets:{target}:S3:ServiceUrl 必須為絕對網址。";
        return string.IsNullOrWhiteSpace(s3.ServiceUrl) && string.IsNullOrWhiteSpace(s3.Region)
            ? $"FileManagement:Targets:{target}:S3 未設定 ServiceUrl 時須設定 Region（AWS S3）。"
            : null;
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

    /// <summary>巢狀設定的數值範圍（ValidateDataAnnotations 不會遞迴驗證巢狀物件）</summary>
    internal static string? ValidateNested(FileManagementOptions o)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        foreach (var (section, value) in new (string, object)[]
                 {
                     ("Upload", o.Upload), ("Image", o.Image), ("ThumbnailCache", o.ThumbnailCache),
                     ("Watermark", o.Watermark), ("Purge", o.Purge)
                 })
        {
            results.Clear();
            if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(value, new(value), results, validateAllProperties: true))
                return $"FileManagement:{section} 設定不正確：{string.Join("；", results.Select(r => r.ErrorMessage))}";
        }

        if (!string.IsNullOrWhiteSpace(o.Watermark.FontPath) && !File.Exists(o.Watermark.FontPath))
            return $"FileManagement:Watermark:FontPath 字型檔不存在（{o.Watermark.FontPath}）。";

        return o.Upload.MaxInMemoryBufferBytes <= o.MaxFileSizeBytes
            ? null
            : "FileManagement:Upload:MaxInMemoryBufferBytes 不可超過全域上限 MaxFileSizeBytes。";
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
