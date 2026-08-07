using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public sealed class SyncUserAdRolesRequest
    {
        [Required(ErrorMessage = "使用者識別為必填項目")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 來自 Windows Authentication 或 LDAP/AD 驗證查詢得到的 AD 群組清單
        /// </summary>
        public List<string> AdGroups { get; set; } = new();
    }
}
