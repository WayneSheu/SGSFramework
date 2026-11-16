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

        /// <summary>結果說明 (例如已排定重新啟動)</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>已排定自動重新啟動 API (重新啟用模組後，以重新初始化模組服務、背景工作與資料庫遷移)</summary>
        public bool RestartScheduled { get; init; }

        /// <summary>預計多少秒後開始重新啟動</summary>
        public int RestartDelaySeconds { get; init; }
    }
}
