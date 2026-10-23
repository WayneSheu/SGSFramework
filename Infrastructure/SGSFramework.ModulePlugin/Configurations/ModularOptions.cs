// Path: src/SGSFramework/Infrastructure/SGSFramework.ModulePlugin/Configurations/ModularOptions.cs
using System.ComponentModel.DataAnnotations;

namespace SGSFramework.ModulePlugin.Configurations;

/// <summary>
/// 動態外掛模組組態 (對應 appsettings 的 ModularSettings)
/// </summary>
public sealed class ModularOptions
{
    public const string SectionName = "ModularSettings";

    /// <summary>
    /// 外掛 DLL 目錄；相對路徑以 AppContext.BaseDirectory 為根
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string PluginsPath { get; set; } = "plugins";

    /// <summary>
    /// 受信任的模組簽章憑證指紋；未設定時系統拒絕啟動以確保安全性
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "未設定受信任的模組簽章憑證指紋 (TrustedThumbprint)，系統拒絕啟動以確保安全性。")]
    public string TrustedThumbprint { get; set; } = string.Empty;
}
