using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs
{
    /// <summary>
    /// 線上人數統計過濾條件 DTO
    /// </summary>
    public sealed record OnlineUserCountQueryDto
    {
        public int? WindowMinutes { get; init; }
    }
}
