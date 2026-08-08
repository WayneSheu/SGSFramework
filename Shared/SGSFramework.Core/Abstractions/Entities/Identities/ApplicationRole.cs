using Microsoft.AspNetCore.Identity;
using SGSFramework.Core.Abstractions.Entities.Base;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 代表應用程式中的角色實體，繼承自 IdentityRole<Guid>，使用 Guid 作為角色的唯一識別碼。
    /// </summary>
    public class ApplicationRole : IdentityRole<Guid>
    {


    }
}
