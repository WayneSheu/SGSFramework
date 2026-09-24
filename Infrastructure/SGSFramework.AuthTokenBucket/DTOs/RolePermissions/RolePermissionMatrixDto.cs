using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.RolePermissions
{
    /// <summary>
    /// 角色權限矩陣DTO
    /// </summary>
    public class RolePermissionMatrixDto
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// 權限鍵清單
        /// </summary>
        public List<string> GrantedPermissionKeys { get; set; } = new();

        /// <summary>
        /// 權限鍵位(Bit表示)清單
        /// </summary>
        public List<int> GrantedBitPositions { get; set; } = new();
    }
}
