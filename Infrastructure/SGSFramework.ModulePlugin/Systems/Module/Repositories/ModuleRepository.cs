using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Modules;
using SGSFramework.ModulePlugin.Abstractions;

namespace SGSFramework.ModulePlugin.Systems.Module.Repositories
{
    /// <summary>
    /// 模組資料存取庫，提供對模組元數據的 CRUD 操作。
    /// </summary>
    /// <typeparam name="TDbContext"></typeparam>
    public class ModuleRepository<TDbContext> : IModuleRepository
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly string _cacheKey;

        public ModuleRepository(TDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
            _cacheKey = $"DynamicRegistry_{typeof(ControllerMetadata).Name}";
        }

        /// <summary>
        /// 根據模組名稱查詢模組元數據。
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<ModuleMetadata?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<ModuleMetadata>()
                .FirstOrDefaultAsync(m => m.ModuleName == moduleName, cancellationToken);
        }

        /// <summary>
        /// 查詢所有模組元數據。
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>

        public async Task<IEnumerable<ModuleMetadata>> GetAllModulesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<ModuleMetadata>()
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 新增或更新模組元數據。如果模組已存在，則更新其版本、組件路徑和最後載入時間；如果不存在，則新增一條記錄。
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task UpsertAsync(ModuleMetadata module, CancellationToken cancellationToken = default)
        {
            var existing = await _dbContext.Set<ModuleMetadata>()
                .FirstOrDefaultAsync(m => m.ModuleName == module.ModuleName, cancellationToken);

            if (existing != null)
            {
                existing.Version = module.Version;
                existing.AssemblyPath = module.AssemblyPath;
                existing.LastLoadedAt = DateTime.UtcNow;
            }
            else
            {
                module.Id = Guid.NewGuid();
                module.LastLoadedAt = DateTime.UtcNow;
                await _dbContext.Set<ModuleMetadata>().AddAsync(module, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// 設定模組的啟用狀態，並同步更新其相關控制器的啟用狀態。
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="isActive"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task SetModuleStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var module = await _dbContext.Set<ModuleMetadata>()
                        .FirstOrDefaultAsync(m => m.ModuleName == moduleName, cancellationToken);
                    if (module != null) module.IsActive = isActive;

                    await _dbContext.Set<ControllerMetadata>()
                        .Where(c => c.ModuleName == moduleName)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, isActive), cancellationToken);

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        /// <summary>
        /// 切換模組的啟用狀態，並同步更新其相關控制器的啟用狀態。
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="isActive"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ToggleModuleStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var module = await _dbContext.Set<ModuleMetadata>()
                        .FirstOrDefaultAsync(m => m.ModuleName == moduleName, cancellationToken);
                    if (module != null) module.IsActive = isActive;

                    await _dbContext.Set<ControllerMetadata>()
                        .Where(c => c.ModuleName == moduleName)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, isActive), cancellationToken);

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    _cache.Remove("DynamicRegistry_ControllerMetadata");
                }
                catch { await transaction.RollbackAsync(cancellationToken); throw; }
            });
        }

        /// <summary>
        /// 刪除指定模組及其相關控制器的元數據，並嘗試刪除對應的組件檔案和 PDB 檔案。
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>

        public async Task RemoveModuleAsync(string moduleName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            string? assemblyPath = null;
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var module = await GetModuleByNameAsync(moduleName, cancellationToken);
                    assemblyPath = module?.AssemblyPath;

                    await DeleteControllersByModuleNameAsync(moduleName, cancellationToken);

                    if (module != null)
                    {
                        await DeleteModuleMetadataAsync(module, cancellationToken);
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });

            _cache.Remove(_cacheKey);

            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
            if (Directory.Exists(pluginsDirectory))
            {
                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    try
                    {
                        var fullPath = Path.IsPathFullyQualified(assemblyPath)
                            ? assemblyPath
                            : Path.Combine(AppContext.BaseDirectory, assemblyPath);

                        if (File.Exists(fullPath))
                        {
                            File.SetAttributes(fullPath, FileAttributes.Normal);
                            File.Delete(fullPath);
                        }

                        var pdbPath = Path.ChangeExtension(fullPath, ".pdb");
                        if (File.Exists(pdbPath))
                        {
                            File.SetAttributes(pdbPath, FileAttributes.Normal);
                            File.Delete(pdbPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FileDelete Warning] 主組件或 PDB 檔案刪除失敗: {ex.Message}");
                    }
                }

                var searchPatternDll = $"{moduleName}*.dll";
                var matchingDlls = Directory.GetFiles(pluginsDirectory, searchPatternDll, SearchOption.TopDirectoryOnly);

                var searchPatternPdb = $"{moduleName}*.pdb";
                var matchingPdbs = Directory.GetFiles(pluginsDirectory, searchPatternPdb, SearchOption.TopDirectoryOnly);

                if (matchingDlls.Length == 0 && moduleName.Contains('.'))
                {
                    var shortName = moduleName.Split('.').Last();
                    matchingDlls = Directory.GetFiles(pluginsDirectory, $"*{shortName}*.dll", SearchOption.TopDirectoryOnly);
                    matchingPdbs = Directory.GetFiles(pluginsDirectory, $"*{shortName}*.pdb", SearchOption.TopDirectoryOnly);
                }

                var allFilesToDelete = matchingDlls.Concat(matchingPdbs).Distinct();

                foreach (var file in allFilesToDelete)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Plugin File Delete Error] 檔案 [{file}] 刪除失敗（可能仍被佔用）: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 刪除指定模組名稱的所有控制器元數據。
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteControllersByModuleNameAsync(string moduleName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            var controllers = await _dbContext.Set<ControllerMetadata>()
                .Where(c => c.ModuleName == moduleName)
                .ToListAsync(cancellationToken);

            if (controllers.Count > 0)
            {
                _dbContext.Set<ControllerMetadata>().RemoveRange(controllers);
            }
        }

        /// <summary>
        /// 刪除指定的模組元數據。
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>

        public async Task DeleteModuleMetadataAsync(ModuleMetadata module, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(module);
            _dbContext.Set<ModuleMetadata>().Remove(module);
            await Task.CompletedTask;
        }
    }
}