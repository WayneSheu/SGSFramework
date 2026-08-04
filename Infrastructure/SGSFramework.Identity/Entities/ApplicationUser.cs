using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Entities
{
    /// <summary>
    /// 擴充自訂的 Identity 使用者實體
    /// </summary>
    public class ApplicationUser : IdentityUser<Guid>
    {
        /// <summary>
        /// 使用者真實姓名
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// 帳號最後登入時間
        /// </summary>
        public DateTimeOffset? LastLoginAt { get; set; }

        /// <summary>
        /// 帳號建立時間
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    }
}
