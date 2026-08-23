using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Strategies
{
    /// <summary>
    /// EfCoreModuleStorageStrategy 提供了基於 Entity Framework Core 的模組元資料儲存策略實現。
    /// </summary>
    /// <typeparam name="TDbContext"></typeparam>
    public class EfCoreModuleStorageStrategy<TDbContext> : IModuleStorageStrategy<TDbContext>
    where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly ILogger<EfCoreModuleStorageStrategy<TDbContext>> _logger;

        public EfCoreModuleStorageStrategy(
            TDbContext dbContext,
            ILogger<EfCoreModuleStorageStrategy<TDbContext>> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task EnsureStorageAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'core')
                BEGIN
                    EXEC(N'CREATE SCHEMA [core]');
                END;";

                await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ">>> [Storage Strategy] 驗證或創建 core Schema 失敗。");
                throw;
            }
        }

        public async Task<ModuleMetadata?> GetByNameAsync(string moduleName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            return await _dbContext.Set<ModuleMetadata>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ModuleName == moduleName, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ModuleMetadata>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<ModuleMetadata>()
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task UpsertAsync(ModuleMetadata module, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(module);

            var existing = await _dbContext.Set<ModuleMetadata>()
                .FirstOrDefaultAsync(m => m.ModuleName == module.ModuleName, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.Version = module.Version;
                existing.AssemblyPath = module.AssemblyPath;
                existing.LastLoadedAt = DateTime.UtcNow;
                _dbContext.Set<ModuleMetadata>().Update(existing);
            }
            else
            {
                if (module.Id == Guid.Empty)
                {
                    module.Id = Guid.NewGuid();
                }
                module.LastLoadedAt = DateTime.UtcNow;
                await _dbContext.Set<ModuleMetadata>().AddAsync(module, cancellationToken).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task SetStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            var strategy = _dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await _dbContext.Set<ModuleMetadata>()
                        .Where(m => m.ModuleName == moduleName)
                        .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsActive, isActive), cancellationToken)
                        .ConfigureAwait(false);

                    await _dbContext.Set<ControllerMetadata>()
                        .Where(c => c.ModuleName == moduleName)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, isActive), cancellationToken)
                        .ConfigureAwait(false);

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogError(ex, ">>> [Storage Strategy] 更新模組狀態失敗: {ModuleName}", moduleName);
                    throw;
                }
            }).ConfigureAwait(false);
        }

        public async Task DeleteAsync(string moduleName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            var strategy = _dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await _dbContext.Set<ControllerMetadata>()
                        .Where(c => c.ModuleName == moduleName)
                        .ExecuteDeleteAsync(cancellationToken)
                        .ConfigureAwait(false);

                    await _dbContext.Set<ModuleMetadata>()
                        .Where(m => m.ModuleName == moduleName)
                        .ExecuteDeleteAsync(cancellationToken)
                        .ConfigureAwait(false);

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogError(ex, ">>> [Storage Strategy] 刪除模組元數據失敗: {ModuleName}", moduleName);
                    throw;
                }
            }).ConfigureAwait(false);
        }
    }
}
