// File: src/SES.Security.Services/WindowsSignatureService.cs
using Microsoft.Extensions.Options;
using SES.CodeSecurity.Helpers;
using SGSFramework.CodeSecurity.Options;


namespace SGSFramework.CodeSecurity.Services.Verifications
{
    /// <summary>
    /// 支援多階段驗證的 WindowsSignatureService。
    /// 驗證流程：1. 驗證模組來源 (ModulePublisher) -> 2. 驗證系統准入 (SystemPublisher)。
    /// </summary>
    public class WindowsSignatureService : ISecurityService
    {
        private readonly IOptionsMonitor<CodeSecurityOptions> _optionsMonitor;

        public WindowsSignatureService(IOptionsMonitor<CodeSecurityOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
        }

        /// <summary>
        /// 執行多階段數位簽章驗證。
        /// </summary>
        public Task<bool> VerifyPluginAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return Task.FromResult(false);

            var options = _optionsMonitor.CurrentValue;

            // 多階段驗證邏輯：
            // 若任何一個階段驗證失敗，則插件視為不安全，拒絕載入。

            // 階段一：驗證開發團隊簽章 (Module)
            bool isModuleVerified = WinTrustHelper.VerifySignature(filePath, options.ModulePublisher);
            if (!isModuleVerified)
            {
                // 可在此處加入 Log：插件未通過模組來源驗證
                return Task.FromResult(false);
            }

            // 階段二：驗證系統准入簽章 (System)
            bool isSystemVerified = WinTrustHelper.VerifySignature(filePath, options.SystemPublisher);
            if (!isSystemVerified)
            {
                // 可在此處加入 Log：插件未通過系統層級准入驗證
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
    }
}