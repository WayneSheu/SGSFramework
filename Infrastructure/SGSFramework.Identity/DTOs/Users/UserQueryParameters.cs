using SGSFramework.Core.Paginations;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary> 
    /// 使用者分頁查詢條件模型 
    /// </summary> 
    public sealed record UserQueryParameters :PagedQueryParameters<UserFilterCriteria>
    {

    }
}
