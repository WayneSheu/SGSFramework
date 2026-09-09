using SGSFramework.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.DTOs
{
    /// <summary>
    /// 切換實驗室執行期上下文結果 DTO
    /// </summary>
    public record SwitchLabResultDto
    {
        /// <summary>
        /// 切換或降級後的使用者權限 Profile 與上下文
        /// </summary>
        public required UserPermissionProfileDto Profile { get; init; }

        /// <summary>
        /// 是否觸發自動降級切換（當目標實驗室無效或未獲授權時為 true）
        /// </summary>
        public bool IsFallback { get; init; }

        /// <summary>
        /// 降級切換時的警告提示訊息
        /// </summary>
        public string? WarningMessage { get; init; }
    }
}
