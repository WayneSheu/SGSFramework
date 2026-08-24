using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Dtos
{
    /// <summary>
    /// 使用者實驗室關聯資料傳輸物件 (DTO)
    /// 搭配 Clean Architecture 與 .NET 10 規範設計，支援精確型別與 Audit 資訊
    /// </summary>
    public sealed record UserLabMappingDto
    {
        /// <summary>
        /// 使用者識別碼 (Guid)
        /// </summary>
        public Guid UserId { get; init; }

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
        public DateTime EffectiveDate { get; init; }

        /// <summary>
        /// 失效日期 (null 代表永久有效)
        /// </summary>
        public DateTime? ExpiryDate { get; init; }

        /// <summary>
        /// 帳號/關聯是否啟用
        /// </summary>
        public bool IsActive { get; init; }

        /// <summary>
        /// 建立時間 (UTC)
        /// </summary>
        public DateTimeOffset CreatedAtUtc { get; init; }

        /// <summary>
        /// 建立者 ID / 帳號
        /// </summary>
        public string? CreatedBy { get; init; }

        /// <summary>
        /// 最後更新時間 (UTC)
        /// </summary>
        public DateTimeOffset? UpdatedAtUtc { get; init; }

        /// <summary>
        /// 最後更新者 ID / 帳號
        /// </summary>
        public string? UpdatedBy { get; init; }
    }
}
