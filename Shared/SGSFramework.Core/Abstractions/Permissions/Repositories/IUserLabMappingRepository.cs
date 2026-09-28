#nullable enable

using SGSFramework;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.Core.Abstractions.Permissions.Repositories;

public interface IUserLabMappingRepository
{
    /// <summary>
    /// 取得用戶的實驗室關聯資訊，用以判定是否為主區域 (IsPrimary)[cite: 1]
    /// </summary>
    Task<UserLabMapping?> GetMappingAsync(Guid userId, int labId, CancellationToken cancellationToken = default);
}