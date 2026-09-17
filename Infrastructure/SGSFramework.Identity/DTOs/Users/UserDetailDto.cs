using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary>
    /// 使用者詳細資料資料傳輸物件 (DTO)
    /// </summary>
    public sealed record UserDetailDto
    {
        /// <summary>
        /// 使用者唯一識別碼
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 使用者帳號名稱
        /// </summary>
        public string Username { get; init; } = string.Empty;

        /// <summary>
        /// 電子郵件地址
        /// </summary>
        public string Email { get; init; } = string.Empty;

        /// <summary>
        /// 電子郵件是否已驗證確認
        /// </summary>
        public bool EmailConfirmed { get; init; }

        /// <summary>
        /// 使用者所擁有的角色清單
        /// </summary>
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    }
}
