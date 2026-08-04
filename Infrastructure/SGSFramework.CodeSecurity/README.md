SGSFramework.CodeSecurity 模組技術文件
1. 概述
本模組提供企業級的數位簽章驗證機制，支援 「多階段簽署」(Multi-stage Signing)。透過與 Windows 原生 WinVerifyTrust API 整合，本模組確保只有經由授權團隊與系統封裝驗證的插件才能被載入，滿足內部軟體供應鏈的資安合規需求。

2. 功能特性
組態驅動註冊：行為邏輯與相依關係由 appsettings.json 管理。

動態監控：支援 IOptionsMonitor，無需重啟即可即時更新驗證規則。

多階段驗證：強制執行兩層次驗證 (模組層級與系統層級)，實現資安責任隔離。

健壯性驗證：整合 Data Annotations 與 ValidateOnStart，確保組態錯誤時快速失敗 (Fail-Fast)。

3. 組態設定 (appsettings.json)
請依據各環境需求設定發行者名稱（採用 CN= 格式）：

JSON
{
  "Security": {
    "Provider": "Windows",
    "ModulePublisher": "CN=Dev-Team-Alpha",
    "SystemPublisher": "CN=Corp-Core-Infrastructure"
  }
}
4. 實作架構
核心元件
ISecurityService：插件驗證介面。

WindowsSignatureService：實作多階段簽章驗證邏輯，整合 IOptionsMonitor 以獲取最新組態。

WinTrustHelper：封裝 P/Invoke 呼叫，處理 Windows Authenticode 驗證與記憶體管理。

驗證流程圖
5. 整合教學
A. 服務註冊
在 Program.cs 或 Startup.cs 中註冊模組：

C#
services.AddSecurityModule(configuration);
B. 插件驗證使用範例
C#
public class PluginLoader
{
    private readonly ISecurityService _securityService;

    public PluginLoader(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    public async Task LoadPluginAsync(string path)
    {
        if (await _securityService.VerifyPluginAsync(path))
        {
            // 插件安全，載入邏輯...
        }
        else
        {
            throw new UnauthorizedAccessException("插件未通過多階段簽章驗證");
        }
    }
}
6. 安全規範與維護
權限控管：模組與系統發行者名稱嚴禁洩漏至非公開儲存庫，建議使用 Azure Key Vault 或環境變數保護。

憑證輪替：若發生憑證外洩，請立即更新 appsettings.json 中的發行者識別名稱，該變更將於幾秒內透過 IOptionsMonitor 在記憶體中生效。

合規稽核：本模組執行驗證時，建議搭配系統級 Logging 以記錄所有失敗的驗證嘗試，作為資安調查之依據。

相關技術參考
[Windows Authenticode 數位簽章概觀]

[P/Invoke 與非受控記憶體管理]

[.NET Options Pattern 與組態監控機制]