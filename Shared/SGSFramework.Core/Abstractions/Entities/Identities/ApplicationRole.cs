using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Base;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 代表應用程式中的角色實體，繼承自 IdentityRole<Guid>，使用 Guid 作為角色的唯一識別碼。
    /// </summary>
    /// <summary>
    /// 自訂應用程式角色實體
    /// </summary>
    public class ApplicationRole : IdentityRole<Guid>,IHasRoleCode
    {
        /// <summary>
        /// 角色代碼 (例如: SYS_ADMIN)
        /// </summary>
        [Unicode(true)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 角色描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}
