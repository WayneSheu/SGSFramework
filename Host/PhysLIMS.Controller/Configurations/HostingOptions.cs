// Path: src/SGSFramework/Host/PhysLIMS.Controller/Configurations/HostingOptions.cs
using System.ComponentModel.DataAnnotations;

namespace SGSFramework.ApiInfrastructure.Configurations;

/// <summary>
/// 部署模式組態 (對應 appsettings 的 DeploymentOptions)
/// </summary>
public sealed class DeploymentOptions
{
    public const string SectionName = "DeploymentOptions";

    /// <summary>
    /// 是否啟用 SPA Hosting (同站台共用部署)；false 為前後端分離部署
    /// </summary>
    public bool EnableSpaHosting { get; set; } = true;
}

/// <summary>
/// 跨域政策組態 (對應 appsettings 的 CorsSettings)。
/// CORS 政策註冊與 <c>CorsLoggingMiddleware</c> 共用此單一來源，避免允許清單不一致。
/// </summary>
public sealed class CorsSettingsOptions
{
    public const string SectionName = "CorsSettings";

    /// <summary>
    /// 政策名稱 (固定值，供 UseCors 使用)
    /// </summary>
    public const string PolicyName = "CorsPolicy";

    [Required]
    [MinLength(1, ErrorMessage = "CorsSettings:AllowedOrigins 至少需設定一個允許來源。")]
    public string[] AllowedOrigins { get; set; } = [];
}

/// <summary>
/// 啟動期資料庫行為組態 (對應 appsettings 的 Database)
/// </summary>
public sealed class DatabaseStartupOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// 啟動時是否自動執行初始化腳本與 EF Core Migration (Development 環境一律執行)
    /// </summary>
    public bool AutoMigrate { get; set; } = true;
}

/// <summary>
/// IIS 託管組態 (對應 appsettings 的 IIS)
/// </summary>
public sealed class IisHostingOptions
{
    public const string SectionName = "IIS";

    [Required(AllowEmptyStrings = false)]
    public string AppPoolName { get; set; } = "PhysLIMS.API";
}
