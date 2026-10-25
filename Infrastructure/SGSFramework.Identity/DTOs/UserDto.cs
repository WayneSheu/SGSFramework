using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public bool LockoutEnabled { get; set; }

        // 【新增】企業欄位擴充
        public string EmployeeNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        // 【新增】所屬主要區域實驗室資訊
        public Guid? PrimaryLabId { get; set; }
        public string? PrimaryLabCode { get; set; }
        public string? PrimaryLabName { get; set; }

        /// <summary>帳號狀態（UserStatus 列舉名稱：Pending / Active / Disabled / Locked）</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>是否因登入失敗過多而處於 Identity 鎖定期間</summary>
        public bool IsLockedOut { get; set; }

        /// <summary>最後登入時間</summary>
        public DateTimeOffset? LastLoginAt { get; set; }

        public List<string> Roles { get; set; } = [];
    }
}
