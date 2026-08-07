using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    // ==========================================
    // Abstractions 層：定義權限初始化與同步服務合約
    // ==========================================
    public interface IPermissionSeedService
    {
        Task SeedAndSyncPermissionsAsync(CancellationToken cancellationToken = default);
    }
}
