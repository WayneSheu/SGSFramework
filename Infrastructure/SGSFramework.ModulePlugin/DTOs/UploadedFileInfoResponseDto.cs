using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.DTOs
{
    public sealed record UploadedFileInfoResponseDto
    {
        public string FileName { get; init; } = string.Empty;
        public long Size { get; init; }
    }
}
