using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Services.Verifications
{
    public interface ISecurityService
    {
        Task<bool> VerifyPluginAsync(string filePath);
    }
}
