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
                    // 標題以程式碼 [Module] 設定為準，讓既有模組也能反映標題異動
                    if (!string.IsNullOrWhiteSpace(moduleInfo.ModuleTitle))
                    {
                        existing.ModuleTitle = moduleInfo.ModuleTitle;
                    }
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
                }

                // 軟停用：該模組的功能資料列 (ControllerMetadata) 標示為不啟用，不刪除。
                // 選單 (MenuItems) 刻意不改：使用者選單查詢會排除「功能不啟用」的頁面，空的區段 / 群組容器自動修剪；
                // 不改選單列可避免與管理者手動隱藏的設定混淆，且啟動同步 (SeedAndSyncMenusAsync) 不會恢復已隱藏的既有節點。
                // 保留 Id、自訂名稱 / 圖示 / 排序、手動停用旗標、使用者個別授權與權限綁定；
                // 重新部署時 DynamicControllerRepository.RegisterAsync 會先全部設為不啟用，再將仍存在於程式碼的功能重新啟用 (保留手動停用決策)。
                // 與模組資料列於同一次 SaveChanges（同一交易）完成，避免只刪一半。
                var controllers = await _dbContext.Set<ControllerMetadata>()
                    .Where(c => c.ModuleName == moduleName && c.IsActive)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                foreach (var controller in controllers)
                {
                    controller.IsActive = false;
                }

                if (module != null || controllers.Count > 0)
                {
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