// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.ModulePlugin/Systems/Controller/Repositories/DynamicControllerRepository.cs
// 架構層級: Infrastructure Layer
// ==========================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Modules;
using SGSFramework.Core.Controllers.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SGSFramework.ModulePlugin.Systems.Controller.Repositories;

public class DynamicControllerRepository<T>(
    DbContext context,
    IMemoryCache cache,
    ILogger<DynamicControllerRepository<T>> logger) : IDynamicControllerRepository<T>
    where T : class, IControllerMetadata
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly DbSet<T> _dbSet = context.Set<T>();
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ILogger<DynamicControllerRepository<T>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _cacheKey = $"DynamicRegistry_{typeof(T).Name}";

    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    public async Task RegisterAsync(string moduleName, IEnumerable<T> controllers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(controllers);

        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();

            // 1. 記憶體去重：防範傳入參數中包含相同 ControllerName + ActionName 的重複項
            var rawControllers = controllers.ToList();
            PreProcessControllers(rawControllers);

            var controllerList = rawControllers
                .GroupBy(c => $"{c.ControllerName?.Trim()}|{c.ActionName?.Trim()}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("[DynamicController] 開始註冊模組: {ModuleName}，傳入且經去重之 Controller/Action 數量: {Count}", moduleName, controllerList.Count);

            try
            {
                var existingEntities = await _dbSet
                    .Where(x => x.ModuleName == moduleName)
                    .ToListAsync();

                _logger.LogInformation("[DynamicController] 資料庫現有 {ModuleName} 模組紀錄共 {Count} 筆。", moduleName, existingEntities.Count);

                // 2. 將既有紀錄預設設為不啟用 (Soft-reset)
                foreach (var entity in existingEntities)
                {
                    entity.IsActive = false;
                    _context.Entry(entity).State = EntityState.Modified;
                }

                // 以 ControllerName + ActionName 建立資料庫既有資料的快速 Index
                var existingDict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
                foreach (var entity in existingEntities)
                {
                    var key = $"{entity.ControllerName?.Trim()}|{entity.ActionName?.Trim()}";
                    existingDict.TryAdd(key, entity);
                }

                int addedCount = 0;
                int updatedCount = 0;

                // 3. 執行 Upsert 比對
                foreach (var controller in controllerList)
                {
                    var controllerKey = $"{controller.ControllerName?.Trim()}|{controller.ActionName?.Trim()}";

                    T? targetEntity = null;

                    if (existingDict.TryGetValue(controllerKey, out var dictEntity))
                    {
                        targetEntity = dictEntity;
                    }
                    else if (!string.IsNullOrWhiteSpace(controller.PermissionKey))
                    {
                        targetEntity = existingEntities.FirstOrDefault(e =>
                            string.Equals(e.PermissionKey?.Trim(), controller.PermissionKey.Trim(), StringComparison.OrdinalIgnoreCase));
                    }

                    if (targetEntity != null)
                    {
                        targetEntity.IsActive = true;
                        _context.Entry(targetEntity).State = EntityState.Modified;

                        var controllerType = GetCachedType(controller.ControllerTypeName);
                        if (controllerType != null && targetEntity is ControllerMetadata metadataEntity)
                        {
                            metadataEntity.SyncFromAttribute(controllerType);
                        }

                        if (targetEntity is ControllerMetadata metaExisting && controller is ControllerMetadata metaNew)
                        {
                            metaExisting.Version = metaNew.Version;
                            metaExisting.RouteTemplate = metaNew.RouteTemplate;
                            metaExisting.ParentMenuName = metaNew.ParentMenuName;
                            metaExisting.ControllerTypeName = metaNew.ControllerTypeName;
                            metaExisting.ControllerName = metaNew.ControllerName;
                            metaExisting.ActionName = metaNew.ActionName;
                            metaExisting.DisplayOrder = metaNew.DisplayOrder;
                            metaExisting.Icon = metaNew.Icon;
                            metaExisting.Description = metaNew.Description;
                            metaExisting.BitPosition = metaNew.BitPosition;           // 同步 BitPosition 欄位
                            metaExisting.AttributesJson = metaNew.AttributesJson;     // 同步 AttributesJson 欄位

                            if (!string.IsNullOrEmpty(metaNew.DisplayName))
                            {
                                metaExisting.DisplayName = metaNew.DisplayName;
                            }
                        }
                        targetEntity.PermissionKey = controller.PermissionKey;
                        updatedCount++;
                    }
                    else
                    {
                        var controllerType = GetCachedType(controller.ControllerTypeName);
                        if (controllerType != null && controller is ControllerMetadata metaNewAdd)
                        {
                            metaNewAdd.SyncFromAttribute(controllerType);

                            var controllerTitleAttr = controllerType.GetCustomAttribute<ControllerTitleAttribute>();
                            var methodInfo = controllerType.GetMethod(metaNewAdd.ActionName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
                            var actionFuncAttr = methodInfo?.GetCustomAttribute<FunctionAttribute>();

                            metaNewAdd.ParentMenuName = controllerTitleAttr?.Title ?? metaNewAdd.ControllerName;
                            metaNewAdd.DisplayOrder = actionFuncAttr?.Order ?? controllerTitleAttr?.Order ?? 0;
                            metaNewAdd.Icon = actionFuncAttr?.Icon ?? controllerTitleAttr?.Icon;
                            metaNewAdd.Description = actionFuncAttr?.Description ?? controllerTitleAttr?.Description;

                            string correctActionDisplayName = actionFuncAttr?.Title ?? metaNewAdd.ActionName;
                            metaNewAdd.DisplayName = correctActionDisplayName;
                        }

                        controller.IsActive = true;
                        await _dbSet.AddAsync(controller);
                        addedCount++;
                    }
                }

                var changesCount = await _context.SaveChangesAsync();
                _logger.LogInformation("[DynamicController] SaveChangesAsync 完成。新增: {AddCount} 筆, 更新/重啟: {UpdCount} 筆, EF 偵測異動總數: {Changes}。", addedCount, updatedCount, changesCount);

                _cache.Remove(_cacheKey);
                _cache.Remove($"{_cacheKey}_AllActive");
                _logger.LogInformation("[DynamicController] 模組 {ModuleName} 註冊與狀態同步處理完畢。", moduleName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DynamicController] 註冊模組 {ModuleName} 時發生未預期錯誤: {Message}", moduleName, ex.Message);
                throw;
            }
        });
    }

    private static void PreProcessControllers(IEnumerable<T> controllers)
    {
        foreach (var controller in controllers)
        {
            var controllerType = GetCachedType(controller.ControllerTypeName);
            if (controllerType != null)
            {
                if (controller is ControllerMetadata metadata)
                {
                    metadata.SyncFromAttribute(controllerType);
                }

                var versionAttr = controllerType.GetCustomAttribute<ApiVersionAttribute>();
                var version = versionAttr?.Version ?? "v1";

                if (controller is ControllerMetadata metaVer)
                {
                    metaVer.Version = version;
                }

                var cleanRoute = controller.RouteTemplate.TrimStart('/');
                var versionPrefixes = Enumerable.Range(1, 20).Select(i => $"v{i}/").ToArray();
                if (versionPrefixes.Any(prefix => cleanRoute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    cleanRoute = string.Join('/', cleanRoute.Split('/').Skip(1));
                }

                if (controller is ControllerMetadata metaRoute)
                {
                    metaRoute.RouteTemplate = $"{version}/{cleanRoute}";
                }

                if (controller is ControllerMetadata metaController)
                {
                    var controllerTitleAttr = controllerType.GetCustomAttribute<ControllerTitleAttribute>();
                    var methodInfo = controllerType.GetMethod(metaController.ActionName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
                    var actionFuncAttr = methodInfo?.GetCustomAttribute<FunctionAttribute>();

                    metaController.ParentMenuName = controllerTitleAttr?.Title ?? metaController.ControllerName;
                    metaController.DisplayOrder = actionFuncAttr?.Order ?? controllerTitleAttr?.Order ?? 0;
                    metaController.Icon = actionFuncAttr?.Icon ?? controllerTitleAttr?.Icon;
                    metaController.Description = actionFuncAttr?.Description ?? controllerTitleAttr?.Description;

                    string correctActionDisplayName = actionFuncAttr?.Title ?? metaController.ActionName;
                    metaController.DisplayName = correctActionDisplayName;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(controller.Version) && controller is ControllerMetadata metaDefault)
                {
                    metaDefault.Version = "v1";
                }
            }
        }
    }

    private static Type? GetCachedType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;

        return TypeCache.GetOrAdd(typeName, name =>
        {
            // 走訪所有 AssemblyLoadContext（包含可回收外掛載入器），避開跨 ALC 繫結例外
            foreach (var alc in System.Runtime.Loader.AssemblyLoadContext.All)
            {
                foreach (var assembly in alc.Assemblies)
                {
                    var type = assembly.GetType(name, false, true);
                    if (type != null) return type;
                }
            }

            return null;
        });
    }

    public async Task UnregisterByModuleAsync(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        try
        {
            var entities = await _dbSet.Where(x => x.ModuleName == moduleName && x.IsActive).ToListAsync();
            foreach (var entity in entities)
            {
                entity.IsActive = false;
                _context.Entry(entity).State = EntityState.Modified;
            }
            await _context.SaveChangesAsync();

            _cache.Remove(_cacheKey);
            _cache.Remove($"{_cacheKey}_AllActive");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DynamicController] 反註冊模組 {ModuleName} 時發生錯誤。", moduleName);
            throw;
        }
    }

    public async Task<IEnumerable<T>> GetActiveControllersAsync()
    {
        return await _cache.GetOrCreateAsync(_cacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return await _dbSet.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        }) ?? [];
    }

    public async Task<IEnumerable<T>> GetAllActiveAsync()
    {
        return await _cache.GetOrCreateAsync($"{_cacheKey}_AllActive", async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return await _dbSet.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        }) ?? [];
    }
}