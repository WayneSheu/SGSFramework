using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.DTOs
{
    public sealed record ToggleStatusResponseDto
    {
        public bool Success { get; init; }
        public string ModuleName { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }
}
