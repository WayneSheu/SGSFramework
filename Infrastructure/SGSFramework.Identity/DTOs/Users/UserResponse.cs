using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    public class UserResponse
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }

        // 【新增】企業欄位擴充
        public string EmployeeNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        // 【新增】所屬主要區域實驗室資訊
        public Guid? PrimaryLabId { get; set; }
        public string? PrimaryLabName { get; set; }
    }
}
