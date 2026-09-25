using MediatR;
using SGSFramework.AuthTokenBucket.DTOs.PermissionUsers;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    public sealed class GetUsersByPermissionKeyQuery : IRequest<PermissionUsersMasterDto>
    {
        /// <summary>
        /// 權限代碼 (例如: SYSTEM.AUTH.LOGINDFMS)
        /// </summary>
        public string PermissionKey { get; set; } = string.Empty;

        /// <summary>
        /// 租戶/實驗室 ID (可選)
        /// </summary>
        public Guid? TenantLabId { get; set; }
    }
}
