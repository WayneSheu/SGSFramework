using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 使用者資訊傳輸物件
    /// </summary>
    public sealed class UserDto
    {
        /// <summary>
        /// 使用者識別碼
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 帳號名稱
        /// </summary>
        public string Username { get; init; } = string.Empty;

        /// <summary>
        /// 電子郵件
        /// </summary>
        public string Email { get; init; } = string.Empty;

        /// <summary>
        /// 電子郵件是否已驗證
        /// </summary>
        public bool EmailConfirmed { get; init; }

        /// <summary>
        /// 是否啟用帳號鎖定機制
        /// </summary>
        public bool LockoutEnabled { get; init; }

        /// <summary>
        /// 使用者所屬角色清單
        /// </summary>
        public List<string> Roles { get; init; } = new();
    }
}
