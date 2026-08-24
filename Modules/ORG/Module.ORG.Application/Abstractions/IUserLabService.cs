using System;
using System.Collections.Generic;
using System.Text;
using SGSFramework.Core.Results;

namespace SGS.Modules.ORG.Application.Abstractions
{
    /// <summary>
    /// 外掛模組之使用者實驗室派駐管理服務 Interface
    /// </summary>
    public interface IUserLabService
    {
        Task<Result> AssignUserToLabAsync(Guid userId, int labId, bool isPrimary, string? jobTitle, CancellationToken cancellationToken = default);
    }
}
