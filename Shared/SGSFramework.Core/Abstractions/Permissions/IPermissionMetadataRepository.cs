using SGSFramework.Core.Abstractions.Permissions.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions
{
    /// <summary>
    /// 權限 Metadata 資料存取介面
    /// </summary>
    public interface IPermissionMetadataRepository
    {
        /// <summary>
        /// 根據權限 Key 集合取得對應的 Metadata 列表
        /// </summary>
        Task<IReadOnlyList<PermissionMetadata>> GetByPermissionKeysAsync(
            IEnumerable<string> permissionKeys,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得系統全量權限 Metadata 列表
        /// </summary>
        Task<IReadOnlyList<PermissionMetadata>> GetAllMetadataAsync(
            CancellationToken cancellationToken = default);
    }
}
