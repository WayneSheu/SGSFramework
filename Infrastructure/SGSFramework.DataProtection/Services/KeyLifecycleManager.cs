using SGSFramework.DataProtection.Abstractions;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;

namespace SGSFramework.DataProtection.Services
{
    public class KeyLifecycleManager
    {
        private readonly IHmsClient _hmsClient;

        public KeyLifecycleManager(IHmsClient hmsClient) => _hmsClient = hmsClient;

        public async Task<bool> IsKeyUsableAsync(string keyAlias)
        {
            var status = await _hmsClient.GetKeyStatusAsync(keyAlias);

            return status switch
            {
                KeyStatus.Active => true,
                KeyStatus.Expired => false,
                KeyStatus.Revoked => throw new SecurityException("Critical: Key has been revoked."),
                KeyStatus.Compromised => throw new SecurityException("ALERT: Key compromise detected."),
                _ => false
            };
        }
    }
}
