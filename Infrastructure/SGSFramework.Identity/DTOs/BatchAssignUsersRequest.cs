using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 批次指派使用者至角色請求物件
    /// </summary>
    public sealed class BatchAssignUsersRequest
    {
        /// <summary>
        /// 要指派的使用者識別碼清單
        /// </summary>
        [Required(ErrorMessage = "使用者識別碼清單不得為空。")]
        public List<string> UserIds { get; init; } = new();
    }
}
