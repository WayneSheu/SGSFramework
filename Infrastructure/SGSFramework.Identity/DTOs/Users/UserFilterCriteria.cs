using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary>
    /// 使用者專用篩選條件模型
    /// </summary>
    public sealed record UserFilterCriteria
    {
        /// <summary>
        /// 實驗室識別碼 (保留原有相容)
        /// </summary>
        public Guid? LabId { get; init; }

        /// <summary>
        /// 帳號是否啟用
        /// </summary>
        public bool? IsActive { get; init; }

        /// <summary>
        /// 員工編號
        /// </summary>
        public string? EmployeeNo { get; init; }

        /// <summary>
        /// 姓名 (對應 Fullname)
        /// </summary>
        public string? Fullname { get; init; }

        /// <summary>
        /// 實驗室代碼 (Level 1 類別，對應 Organization.Parent 的 Code；精確比對)
        /// </summary>
        public string? LaboratoryParent { get; init; }

        /// <summary>
        /// 區域實驗室代碼 (Level 2，對應 Organization 的 Code；精確比對)
        /// </summary>
        public string? RegionalLaboratoryCode { get; init; }
    }
}
