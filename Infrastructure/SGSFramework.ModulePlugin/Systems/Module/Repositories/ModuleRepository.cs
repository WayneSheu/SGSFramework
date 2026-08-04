using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Modules;
using SGSFramework.Core.Abstractions.Entities.Controller;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using SGSFramework.ModulePlugin.Abstractions;

namespace SGSFramework.ModulePlugin.Systems.Module.Repositories
{

    /// <summary>
    /// 泛型模組倉儲實作，使用泛型 DbContext 以支援不同的資料庫上下文
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

        public async Task<ModuleMetadata?> GetModuleByNameAsync(string moduleName)
        {
            return await _dbContext.Set<ModuleMetadata>()
                .FirstOrDefaultAsync(m => m.ModuleName == moduleName);
        }

        /// <summary>
        /// 取得所有模組的清單資料
        /// </summary>
        public async Task<IEnumerable<ModuleMetadata>> GetAllModulesAsync()
        {
            return await _dbContext.Set<ModuleMetadata>()
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// 新增或更新模組資料到資料庫中
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public async Task UpsertAsync(ModuleMetadata module)
        {
            var existing = await _dbContext.Set<ModuleMetadata>()
                .FirstOrDefaultAsync(m => m.ModuleName == module.ModuleName);

            if (existing != null)
            {

                // 更新邏輯
                existing.Version = module.Version;
                existing.AssemblyPath = module.AssemblyPath;
                existing.LastLoadedAt = DateTime.UtcNow;
              
            }
            else
            {
                // 新增邏輯
                module.Id = Guid.NewGuid();
                module.LastLoadedAt = DateTime.UtcNow;
                await _dbContext.Set<ModuleMetadata>().AddAsync(module);
            }

            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// 更新模組狀態
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="isActive"></param>
        /// <returns></returns>
        public async Task SetModuleStatusAsync(string moduleName, bool isActive)
        {
            // 1. 取得執行策略
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            // 2. 使用策略來執行整個 Transaction 區塊
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    // 更新模組主表
                    var module = await _dbContext.Set<ModuleMetadata>()
                        .FirstOrDefaultAsync(m => m.ModuleName == moduleName);
                    if (module != null) module.IsActive = isActive;

                    // 更新 Controller 狀態
                    await _dbContext.Set<ControllerMetadata>()
                        .Where(c => c.ModuleName == moduleName)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, isActive));

                    await _dbContext.SaveChangesAsync();

                    // 提交交易
                    await transaction.CommitAsync();
                }
                catch
                {
                    // 交易失敗時由策略層決定是否重試
                    await transaction.RollbackAsync();
                    throw;
                }
            });

        }

        /// <summary>
        /// 切換模組狀態（啟用/停用）
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="isActive"></param>
        /// <returns></returns>
        public async Task ToggleModuleStatusAsync(string moduleName, bool isActive)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    // 1. 更新模組狀態
                    var module = await _dbContext.Set<ModuleMetadata>()
                        .FirstOrDefaultAsync(m => m.ModuleName == moduleName);
                    if (module != null) module.IsActive = isActive;

                    //更新該模組下的所有 Controller 狀態
                    await _dbContext.Set<ControllerMetadata>()
                        .Where(c => c.ModuleName == moduleName)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, isActive));

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    //清除快取，讓前端與後端路由同步更新
                    _cache.Remove("DynamicRegistry_ControllerMetadata");
                }
                catch { await transaction.RollbackAsync(); throw; }
            });
        }


        //實作完整資料庫同步與刪除邏輯
        public async Task RemoveModuleAsync(string moduleName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            string? assemblyPath = null;
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            // 1. 執行資料庫交易與元資料刪除
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    var module = await GetModuleByNameAsync(moduleName);
                    assemblyPath = module?.AssemblyPath;

                    await DeleteControllersByModuleNameAsync(moduleName);

                    if (module != null)
                    {
                        await DeleteModuleMetadataAsync(module);
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            // 2. 清除記憶體快取
            _cache.Remove(_cacheKey);

            // 3. 執行多次記憶體回收，確保 AssemblyLoadContext 徹底釋放參考與檔案鎖定
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // 4. 執行實體檔案刪除（同時清除 .dll 與對應的 .pdb 檔案）
            var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
            if (Directory.Exists(pluginsDirectory))
            {
                // 處理主組件及其 PDB
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

                        // 同步刪除主組件對應的 PDB 檔案
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

                // 搜尋並清理所有相關的 .dll 與 .pdb 檔案
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

                // 合併所有需要清除的目標檔案（包含 Application 與 Infrastructure 的 dll/pdb）
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
        /// 透過倉儲刪除指定模組底下的所有控制器元資料
        /// </summary>
        public async Task DeleteControllersByModuleNameAsync(string moduleName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

            var controllers = await _dbContext.Set<ControllerMetadata>()
                .Where(c => c.ModuleName == moduleName)
                .ToListAsync();

            if (controllers.Count > 0)
            {
                _dbContext.Set<ControllerMetadata>().RemoveRange(controllers);
            }
        }

        /// <summary>
        /// 透過倉儲刪除模組元資料
        /// </summary>
        public async Task DeleteModuleMetadataAsync(ModuleMetadata module)
        {
            ArgumentNullException.ThrowIfNull(module);
            _dbContext.Set<ModuleMetadata>().Remove(module);
            await Task.CompletedTask;
        }

        // 將具體的 SESDbContext 綁定到泛型 Repository 實作
        //builder.Services.AddScoped<IModuleRepository>(sp => 
        //{
        //    var context = sp.GetRequiredService<SESDbContext>();
        //    return new ModuleRepository<SESDbContext>(context);
        //});

    }
}
