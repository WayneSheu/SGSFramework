using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Repositories
{
    /// <summary>
    /// 權限 Metadata Repository 的 EF Core 實作
    /// </summary>
    public sealed class PermissionMetadataRepository<TDbContext> : IPermissionMetadataRepository
        where TDbContext : DbContext, ITokenDbContext
    {
        private readonly TDbContext _dbContext;

        public PermissionMetadataRepository(TDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PermissionMetadata>> GetByPermissionKeysAsync(
            IEnumerable<string> permissionKeys,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(permissionKeys);

            var keysList = permissionKeys as List<string> ?? permissionKeys.ToList();
            if (keysList.Count == 0)
            {
                return Array.Empty<PermissionMetadata>();
            }

            return await _dbContext.Set<PermissionMetadata>()
                .AsNoTracking()
                .Where(m => m.PermissionKey != null && keysList.Contains(m.PermissionKey))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PermissionMetadata>> GetAllMetadataAsync(
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<PermissionMetadata>()
                .AsNoTracking()
                .Where(m => !string.IsNullOrEmpty(m.PermissionKey))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
