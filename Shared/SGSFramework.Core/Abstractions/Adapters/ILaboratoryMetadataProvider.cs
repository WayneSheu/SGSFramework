using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Adapters
{
    /// <summary>
    /// 實驗室元數據提供者接口
    /// 用以解耦對特定業務模組（ORG）資料庫實體的直接相依。
    /// </summary>
    public interface ILaboratoryMetadataProvider
    {
        /// <summary>
        /// 取得實驗室的業務屬性（如類別與部門代碼）
        /// </summary>
        Task<LaboratoryMetadataDto?> GetLaboratoryMetadataAsync(int labId, CancellationToken cancellationToken = default);
        Task<LaboratoryMetadataDto?> GetLaboratoryMetadataAsync(Guid tenantLabId, CancellationToken cancellationToken = default);
    }

    public sealed record LaboratoryMetadataDto(
        int LabId,
        Guid TenantLabId,
        string? Category,
        string? DepartmentCode,
        string? Code,
        bool IsActive
    );
}
