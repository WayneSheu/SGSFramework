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

        /// <summary>已排定自動重新啟動 API（新模組於重啟後註冊服務並執行資料庫遷移）</summary>
        public bool RestartScheduled { get; init; }

        /// <summary>預計多少秒後開始重新啟動</summary>
        public int RestartDelaySeconds { get; init; }
        public IEnumerable<UploadedFileInfoResponseDto> Files { get; init; } = Enumerable.Empty<UploadedFileInfoResponseDto>();
    }
}
