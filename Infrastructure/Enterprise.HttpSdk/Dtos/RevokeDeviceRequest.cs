using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Dtos
{
    /// <summary>
    /// Revoke device request DTO. Used to revoke a specific device's access.
    /// </summary>
    public sealed class RevokeDeviceRequest
    {
        public string DeviceId { get; set; } = string.Empty;
    }
}
