using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.DTOs
{
    public sealed record ModuleUploadResponseDto
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string ModuleName { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public IEnumerable<UploadedFileInfoResponseDto> Files { get; init; } = Enumerable.Empty<UploadedFileInfoResponseDto>();
    }
}
