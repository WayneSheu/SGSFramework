using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ApiInfrastructure.DTOs
{
    /// <summary>
    /// 變更功能停用/啟用狀態之請求
    /// </summary>
    public record UpdateFunctionStatusRequest
    {
        /// <summary>
        /// 是否啟用功能
        /// </summary>
        public bool IsActive { get; init; }

        /// <summary>
        /// 變更原因或註記 (選擇性)
        /// </summary>
        public string? Reason { get; init; }
    }
}
