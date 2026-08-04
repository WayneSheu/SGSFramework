using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Abstractions
{
    /// <summary>
    /// 簽署請求模型
    /// </summary>
    public record SigningRequest(string FilePath, string CertificateThumbprint, string? Description);
}
 