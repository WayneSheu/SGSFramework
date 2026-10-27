using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Mask;
using SGSFramework.Core.Abstractions.Entities.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 擴充自訂的 Identity 使用者實體
    /// </summary>
    [DisplayName("擴充自訂的 Identity 使用者實體")]
    [Comment("擴充自訂的 Identity 使用者實體")]
    public class ApplicationUser : IdentityUser<Guid>, IBaseUser, IAuditable
    {
        [DisplayName("是否為系統管理員")]
        [Comment("是否為系統管理員")]
        public bool IsSystemAdmin { get; set; }=false;

        [DisplayName("員工編號")]
        [Comment("員工編號")]   
        public string? EmployeeNo { get; set; } =string.Empty;

        /// <summary>
        /// 使用者真實姓名
        /// </summary>
        [DisplayName("使用者真實姓名")]
        [Comment("使用者真實姓名")]
        [Masked(MaskFormat.Name)] // 需支援搜尋/排序，故僅遮蔽日誌、不落地加密
        public string FullName { get; set; } = string.Empty;


        /// <summary>
        /// 帳號目前狀態
        /// </summary>
        [DisplayName("帳號目前狀態")]
        [Comment("帳號目前狀態")]
        public UserStatus Status { get; set; } = UserStatus.Active;

        /// <summary>
        /// 帳號最後登入時間
        /// </summary>
        [DisplayName("帳號最後登入時間")]
        [Comment("帳號最後登入時間")]
        public DateTimeOffset? LastLoginAt { get; set; }

       
        /// <summary>
        /// 帳號建立時間
        /// </summary>
        [DisplayName("帳號建立時間")]
        [Comment("帳號建立時間")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [DisplayName("帳號建立者")]
        [Comment("帳號建立者")]  
        public bool IsDeleted { get; set; } = false;

        [DisplayName("帳號刪除者")]
        [Comment("帳號刪除者")]  
        public string DeletedBy { get; set; } = string.Empty;

        [DisplayName("帳號刪除時間")]
        [Comment("帳號刪除時間")] 
        public DateTimeOffset? DeletedAt { get; set; }

        // --- IAuditable 實作 ---
        // 由 AuditInterceptor 於 SaveChanges 時自動填入；[AuditIgnore] 已標於介面成員，不會寫入資料日誌
        [Editable(false)]
        [MaxLength(256)]
        [DisplayName("建立者")]
        [Comment("建立者")]
        public string CreatedBy { get; set; } = string.Empty;

        [Editable(false)]
        [DisplayName("建立時間 (UTC)")]
        [Comment("建立時間 (UTC)")]
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        [MaxLength(256)]
        [DisplayName("最後修改者")]
        [Comment("最後修改者")]
        public string? UpdatedBy { get; set; }

        [DisplayName("最後修改時間 (UTC)")]
        [Comment("最後修改時間 (UTC)")]
        public DateTimeOffset? UpdatedAtUtc { get; set; }


        /// <summary>
        /// 檢查帳號當前是否允許正常登入與操作
        /// </summary>
        public bool CanLogin()
        {
            return !IsDeleted && Status == UserStatus.Active;
        }


    }
}
