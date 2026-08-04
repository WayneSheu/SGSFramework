using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.DTOs
{
    /// <summary>
    /// 模組完整詳細資訊回應模型
    /// </summary>
    public sealed class ModuleDetailResponse
    {
        public string ModuleName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string AssemblyPath { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? LastLoadedAt { get; set; }
        public string? Checksum { get; set; }
    }
}
