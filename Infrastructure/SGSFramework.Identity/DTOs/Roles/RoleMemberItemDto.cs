using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Roles
{
    public class RoleMemberItemDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string EmployeeNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        // 【新增】主區域實驗室資訊
        public Guid? PrimaryLabId { get; set; }
        public string? PrimaryLabCode { get; set; }
        public string? PrimaryLabName { get; set; }

        /// <summary>過渡期到期時間；null 表示永久成員</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>過渡期原因</summary>
        public string? ExpirationReason { get; set; }
    }
}
