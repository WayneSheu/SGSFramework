using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Abstractions
{
    /// <summary>
    /// 定義無密碼認證提供者介面
    /// </summary>
    public interface IPasswordlessAuthenticator
    {
        Task<bool> ValidateAssertionAsync(string credentialId, string clientData, string signature);
    }
}
