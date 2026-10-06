using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SPAModulePlugin.Domain.Abstractions
{
    public interface ISPAModuleSecurityVerifier
    {
        /// <summary>
        /// 驗證前端模組檔案之 SHA-256 完整性與數位簽章
        /// </summary>
        /// <param name="filePath">模組實體檔案路徑</param>
        /// <param name="expectedHash">預期 SHA-256 雜湊</param>
        /// <returns>驗證是否通過</returns>
        bool VerifyModuleIntegrity(string filePath, string expectedHash);
    }
}
