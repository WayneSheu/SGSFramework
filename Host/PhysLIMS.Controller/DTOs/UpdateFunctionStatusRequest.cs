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
        [System.ComponentModel.DataAnnotations.MaxLength(500)]
        public string? Reason { get; init; }

        /// <summary>
        /// 對使用者的公告內容 (停用時顯示於系統公告與 API 拒絕訊息；未填則使用預設文字)
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(500)]
        public string? Notice { get; init; }

        /// <summary>
        /// 預計恢復時間 (僅供公告，不會自動恢復)
        /// </summary>
        public DateTimeOffset? ExpectedRestoreAt { get; init; }
    }
}
