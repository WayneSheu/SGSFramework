using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionUsers
{
    public class UserLabDto
    {
        /// <summary>
        /// 實驗室類別代碼 (string)
        /// </summary>
        public string CategoryCode { get; set; } = string.Empty;


        /// <summary>
        /// 實驗室類別名稱 (string)
        /// </summary>
        public string CategoryName { get; set; } = string.Empty;


        /// <summary>
        /// 實驗室識別碼 (int)
        /// </summary>
        public int LabId { get; init; }

        /// <summary>
        /// 租戶實驗室識別碼 (Guid)
        /// </summary>
        public Guid TenantLabId { get; init; }

        /// <summary>
        /// 實驗室名稱 (由 Laboratory 實體 Join 解析)
        /// </summary>
        public string LabName { get; init; } = string.Empty;

        /// <summary>
        /// 實驗室編號/代碼
        /// </summary>
        public string LabCode { get; init; } = string.Empty;

        /// <summary>
        /// 是否為主要歸屬實驗室
        /// </summary>
        public bool IsPrimary { get; init; }

        /// <summary>
        /// 職位標題
        /// </summary>
        public string? JobTitle { get; init; }

        /// <summary>
        /// 生效日期
        /// </summary>
        public DateTimeOffset EffectiveDate { get; init; }

        /// <summary>
        /// 失效日期 (null 代表永久有效)
        /// </summary>
        public DateTimeOffset? ExpiryDate { get; init; }


    }
}
