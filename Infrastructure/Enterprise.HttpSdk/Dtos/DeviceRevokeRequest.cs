using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Enterprise.HttpSdk.Dtos
{
    public sealed class DeviceRevokeRequest
    {
        [Required]
        public string DeviceId { get; set; } = string.Empty;
    }
}
