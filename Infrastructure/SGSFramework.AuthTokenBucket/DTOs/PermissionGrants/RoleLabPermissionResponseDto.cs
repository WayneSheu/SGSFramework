using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionGrants
{
    /// <summary>
    /// 角色在特定實驗室下的 BitMask 權限矩陣回應 DTO
    /// </summary>
    public sealed record RoleLabPermissionResponseDto
    {
        public Guid RoleId { get; init; }
        public Guid LabId { get; init; }

        /// <summary>
        /// 已賦予的 BitPosition 陣列清單
        /// </summary>
        public IEnumerable<int> GrantedBitPositions { get; init; } = [];

        /// <summary>
        /// Base64 編碼之 raw BitmaskVector
        /// </summary>
        public string PermissionVectorBase64 { get; init; } = string.Empty;
    }
}
