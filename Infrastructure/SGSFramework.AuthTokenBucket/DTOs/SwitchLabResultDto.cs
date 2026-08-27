using SGSFramework.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    public sealed record SwitchLabResultDto
    {
        public required UserPermissionProfileDto Profile { get; init; }
        public bool IsFallback { get; init; }
        public string? WarningMessage { get; init; }
    }
}
