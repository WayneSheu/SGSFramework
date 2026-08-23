using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Modules;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.Systems.Module.Services;
using SGSFramework.ModulePlugin.Systems.Module.Strategies;

namespace SGSFramework.ModulePlugin.Systems.Module.Repositories
{
    /// <summary>
    /// ModuleRepository 提供了模組元資料的存取與管理功能，並整合了模組儲存策略與檔案存儲服務。
    /// </summary>
    /// <typeparam name="TDbContext"></typeparam>
    /// <summary>
    /// 基於 EF Core 與 MemoryCache 的模組倉儲實作
    /// </summary>
    /// <typeparam name="TDbContext">目標 EF Core DbContext 類型</typeparam>
    public class ModuleRepository<TDbContext> : IModuleRepository
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly DbSet<ModuleMetadata> _dbSet;
        private const string CacheKeyPrefix = "ModuleInfo_";

        public ModuleRepository(TDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _dbSet = _dbContext.Set<ModuleMetadata>();
        }

        public async Task<IEnumerable<ModuleMetadata>> GetAllModulesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<ModuleMetadata?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            var cacheKey = $"{CacheKeyPrefix}{moduleName.ToLowerInvariant()}";

            if (_cache.TryGetValue(cacheKey, out ModuleMetadata? cachedModule))
            {
                return cachedModule;
            }

            var module = await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(m => m.ModuleName == moduleName, cancellationToken)
                .ConfigureAwait(false);

            if (module != null)
            {
                _cache.Set(cacheKey, module, TimeSpan.FromMinutes(30));
            }

            return module;
        }

        public async Task UpsertAsync(ModuleMetadata moduleInfo, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(moduleInfo);

            try
            {
                var existing = await _dbSet
                    .FirstOrDefaultAsync(m => m.ModuleName == moduleInfo.ModuleName, cancellationToken)
                    .ConfigureAwait(false);

                if (existing == null)
                {
                    await _dbSet.AddAsync(moduleInfo, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    existing.Version = moduleInfo.Version;
                    existing.AssemblyPath = moduleInfo.AssemblyPath;
                    existing.IsActive = moduleInfo.IsActive;
                    existing.LastLoadedAt = moduleInfo.LastLoadedAt;
                    existing.Checksum = moduleInfo.Checksum;
                    _dbSet.Update(existing);
                }

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                InvalidateCache(moduleInfo.ModuleName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"更新模組 [{moduleInfo.ModuleName}] 元資料時發生錯誤。", ex);
            }
        }

        public async Task ToggleModuleStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            try
            {
                var module = await _dbSet
                    .FirstOrDefaultAsync(m => m.ModuleName == moduleName, cancellationToken)
                    .ConfigureAwait(false);

                if (module == null)
                {
                    throw new KeyNotFoundException($"找不到模組 [{moduleName}] 的資料庫紀錄。");
                }

                module.IsActive = isActive;
                module.LastLoadedAt = DateTime.UtcNow;

                _dbSet.Update(module);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                InvalidateCache(moduleName);
            }
            catch (Exception ex) when (ex is not KeyNotFoundException)
            {
                throw new InvalidOperationException($"切換模組 [{moduleName}] 啟用狀態為 [{isActive}] 時發生錯誤。", ex);
            }
        }

        public async Task RemoveModuleAsync(string moduleName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            try
            {
                var module = await _dbSet
                    .FirstOrDefaultAsync(m => m.ModuleName == moduleName, cancellationToken)
                    .ConfigureAwait(false);

                if (module != null)
                {
                    _dbSet.Remove(module);
                    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                InvalidateCache(moduleName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"移除模組 [{moduleName}] 時發生錯誤。", ex);
            }
        }

        private void InvalidateCache(string moduleName)
        {
            var cacheKey = $"{CacheKeyPrefix}{moduleName.ToLowerInvariant()}";
            _cache.Remove(cacheKey);
        }
    }
}