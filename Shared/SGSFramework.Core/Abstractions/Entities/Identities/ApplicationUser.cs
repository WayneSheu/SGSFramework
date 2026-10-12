using Microsoft.AspNetCore.Identity;
using SGSFramework.Core.Abstractions.Entities.Base;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 擴充自訂的 Identity 使用者實體
    /// </summary>
    public class ApplicationUser : IdentityUser<Guid>,IBaseUser
    {
        public bool IsSystemAdmin { get; set; }=false;


        public string? EmployeeNo { get; set; } =string.Empty;

        /// <summary>
        /// 使用者真實姓名
        /// </summary>
        public string FullName { get; set; } = string.Empty;


        /// <summary>
        /// 帳號目前狀態
        /// </summary>
        public UserStatus Status { get; set; } = UserStatus.Active;

        /// <summary>
        /// 帳號最後登入時間
        /// </summary>
        public DateTimeOffset? LastLoginAt { get; set; }

       
        /// <summary>
        /// 帳號建立時間
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public string DeletedBy { get; set; } = string.Empty;

        public DateTimeOffset? DeletedAt { get; set; }


        /// <summary>
        /// 檢查帳號當前是否允許正常登入與操作
        /// </summary>
        public bool CanLogin()
        {
            return !IsDeleted && Status == UserStatus.Active;
        }


    }
}
