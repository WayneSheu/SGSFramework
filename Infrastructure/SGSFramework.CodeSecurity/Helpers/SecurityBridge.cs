using SES.CodeSecurity.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Helpers
{
    public static class SecurityBridge
    {
        // 提供一個公開的靜態委派，供跨 Context 的 ModuleLoader 調用
        public static bool VerifyModule(string path, string modulePub, string systemPub)
        {
            return WinTrustHelper.VerifySignature(path, modulePub)
                && WinTrustHelper.VerifySignature(path, systemPub);
        }
    }
}
