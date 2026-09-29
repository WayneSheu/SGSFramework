using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionAudits
{
    /// <summary>
    /// 權限明細稽核 DTO (包含名稱與說明)
    /// </summary>
    public sealed class PermissionAuditInfoItemDto
    {
        /// <summary>
        /// 權限鍵值/代碼 (例如: SYSTEM.USERMANAGEMENT.READ)
        /// </summary>
        public string PermissionKey { get; set; } = string.Empty;

        /// <summary>
        /// 權限顯示標題 / 名稱
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 權限詳細描述
        /// </summary>
        public string? Description { get; set; }
    }
}
