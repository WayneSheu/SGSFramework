using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions.Contract
{
    /// <summary>
    /// 實理實驗室存取服務業務領域合約
    /// </summary>
    public interface ILaboratoryAccessService
    {
        Task<bool> ValidateUserLaboratoryAccessAsync(Guid userId, Guid laboratoryId, CancellationToken cancellationToken = default);
        Task<bool> ValidateLaboratoryCategoryAsync(Guid laboratoryId, string[] allowedCategories, CancellationToken cancellationToken = default);
    }
}
