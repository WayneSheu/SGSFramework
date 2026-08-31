using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionGrants
{
    /// <summary>
    /// 更新角色在特定實驗室下的權限請求 DTO
    /// </summary>
    public sealed record UpdateRolePermissionsRequestDto
    {
        /// <summary>
        /// 勾選要啟用的 BitPosition 列表
        /// </summary>
        public IEnumerable<int> GrantedBitPositions { get; init; } = [];
    }
}
