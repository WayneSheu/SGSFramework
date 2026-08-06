using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    /// <summary>
    /// 線上人數統計過濾條件 DTO
    /// </summary>
    public sealed record OnlineUserCountQueryDto
    {
        public int? WindowMinutes { get; init; }
    }
}
