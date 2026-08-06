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
        public Guid TargetLabId { get; init; }
    }
}
