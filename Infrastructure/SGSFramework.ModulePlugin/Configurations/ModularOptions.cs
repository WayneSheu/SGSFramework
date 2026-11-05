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

    /// <summary>
    /// 上傳模組後是否自動重新啟動 API。外掛的 DI 註冊與資料庫遷移（含獨立資料庫建置）只在啟動時執行，
    /// 不重啟則新模組不會生效。IIS 會於下一個請求（或 AlwaysRunning 時立即）重新啟動應用程式；
    /// 本機 dotnet run / Visual Studio 偵錯沒有重啟機制，停止後須手動啟動，開發環境建議關閉。
    /// </summary>
    public bool RestartAfterUpload { get; set; } = true;

    /// <summary>自動重啟前的等待秒數（讓上傳回應先送達用戶端）</summary>
    [Range(1, 60)]
    public int RestartDelaySeconds { get; set; } = 3;
}
