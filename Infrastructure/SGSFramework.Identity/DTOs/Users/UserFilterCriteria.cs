using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary>
    /// 使用者專用篩選條件模型
    /// </summary>
    public sealed record UserFilterCriteria
    {
        public Guid? LabId { get; init; }
        public bool? IsActive { get; init; }
    }
}
