using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary>
    /// 使用者配置上下文資料模型
    /// </summary>
    public record UserProvisioningContext(
        string Username,
        string Email,
        string? Password,
        int DefaultLabId,
        Guid TenantLabId,
        string RoleName
    );
}
