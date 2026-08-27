using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    /// <summary>
    /// 實驗室上下文切換請求 DTO
    /// </summary>
    public sealed record SwitchLabRequestDto
    {
        /// <summary>
        /// 目標實驗室識別碼 (可空，若為 null 或無權存取將自動退路至主實驗室)
        /// </summary>
        public Guid? TargetLabId { get; init; }
    }
}
