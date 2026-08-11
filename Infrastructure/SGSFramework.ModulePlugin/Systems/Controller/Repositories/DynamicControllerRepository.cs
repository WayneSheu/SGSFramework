using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Modules;
using SGSFramework.Core.Controllers.Services;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SGSFramework.ModulePlugin.Systems.Controller.Repositories
{
    public class DynamicControllerRepository<T> : IDynamicControllerRepository<T> where T : class, IControllerMetadata
    {
        private readonly DbContext _context;
        private readonly DbSet<T> _dbSet;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DynamicControllerRepository<T>> _logger;
        private readonly string _cacheKey;

        public DynamicControllerRepository(DbContext context, IMemoryCache cache, ILogger<DynamicControllerRepository<T>> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheKey = $"DynamicRegistry_{typeof(T).Name}";
        }

        public async Task RegisterAsync(string moduleName, IEnumerable<T> controllers)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();

                var controllerList = controllers.ToList();
                _logger.LogInformation("[DynamicController] 開始註冊模組: {ModuleName}，傳入 Controller 數量: {Count}", moduleName, controllerList.Count);

                try
                {
                    PreProcessControllers(controllerList);

                    var existingEntities = await _dbSet
                        .Where(x => x.ModuleName == moduleName)
                        .ToListAsync();

                    _logger.LogInformation("[DynamicController] 資料庫現有 {ModuleName} 模組紀錄共 {Count} 筆。", moduleName, existingEntities.Count);

                    int resetCount = 0;
                    foreach (var entity in existingEntities)
                    {
                        if (entity.IsActive)
                        {
                            entity.IsActive = false;
                            _context.Entry(entity).State = EntityState.Modified;
                            resetCount++;
                        }
                    }
                    _logger.LogInformation("[DynamicController] 已將 {Count} 筆舊有啟用紀錄在記憶體中標記為失效。", resetCount);

                    var existingDict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entity in existingEntities)
                    {
                        var key = $"{GetCleanTypeName(entity.ControllerTypeName)}|{entity.ActionName?.Trim()}";
                        if (!existingDict.ContainsKey(key))
                        {
                            existingDict[key] = entity;
                        }
                    }

                    int addedCount = 0;
                    int updatedCount = 0;

                    foreach (var controller in controllerList)
                    {
                        var cleanIncomingName = GetCleanTypeName(controller.ControllerTypeName);
                        var actionName = controller.ActionName?.Trim();
                        var key = $"{cleanIncomingName}|{actionName}";

                        T? targetEntity = null;

                        if (existingDict.TryGetValue(key, out var dictEntity))
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

                            var controllerType = Type.GetType(controller.ControllerTypeName);
                            if (controllerType != null)
                            {
                                (targetEntity as ControllerMetadata)?.SyncFromAttribute(controllerType);
                            }

                            if (targetEntity is ControllerMetadata metaExisting && controller is ControllerMetadata metaNew)
                            {
                                metaExisting.Version = metaNew.Version;
                                metaExisting.RouteTemplate = metaNew.RouteTemplate;
                                metaExisting.ParentMenuName = metaNew.ParentMenuName;
                                metaExisting.ControllerTypeName = metaNew.ControllerTypeName;
                                metaExisting.ControllerName = metaNew.ControllerName;
                                metaExisting.DisplayOrder = metaNew.DisplayOrder;

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
                            var controllerType = Type.GetType(controller.ControllerTypeName);
                            if (controllerType != null && controller is ControllerMetadata metaNewAdd)
                            {
                                metaNewAdd.SyncFromAttribute(controllerType);

                                var controllerMenuAttr = controllerType.GetCustomAttribute<MenuAttribute>();
                                if (controllerMenuAttr != null && !string.IsNullOrEmpty(controllerMenuAttr.Name))
                                {
                                    metaNewAdd.ParentMenuName = controllerMenuAttr.Name;
                                }

                                var methodInfo = controllerType.GetMethod(metaNewAdd.ActionName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
                                var actionMenuAttr = methodInfo?.GetCustomAttribute<MenuAttribute>();

                                string parentMenuName = null;
                                if (actionMenuAttr != null && !string.IsNullOrEmpty(actionMenuAttr.Parent))
                                {
                                    parentMenuName = actionMenuAttr.Parent;
                                }
                                else if (string.Equals(metaNewAdd.ActionName, "Index", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(metaNewAdd.ActionName, "INDEX", StringComparison.OrdinalIgnoreCase))
                                {
                                    parentMenuName = controllerMenuAttr?.Parent;
                                }
                                else
                                {
                                    parentMenuName = controllerMenuAttr?.Name;
                                }

                                if (!string.IsNullOrEmpty(parentMenuName))
                                {
                                    metaNewAdd.ParentMenuName = parentMenuName;
                                }

                                // 確保 DisplayOrder 正確對應 Action 或 Controller 的 MenuAttribute.Order
                                if (actionMenuAttr != null)
                                {
                                    metaNewAdd.DisplayOrder = actionMenuAttr.Order;
                                }
                                else if (controllerMenuAttr != null)
                                {
                                    metaNewAdd.DisplayOrder = controllerMenuAttr.Order;
                                }

                                var actionDisplayAttr = methodInfo?.GetCustomAttribute<DisplayAttribute>();
                                string correctActionDisplayName = actionMenuAttr?.Name ?? actionDisplayAttr?.Name;

                                if (!string.IsNullOrEmpty(correctActionDisplayName))
                                {
                                    metaNewAdd.DisplayName = correctActionDisplayName;
                                }
                                else if (metaNewAdd.DisplayName == metaNewAdd.ControllerName)
                                {
                                    metaNewAdd.DisplayName = metaNewAdd.ActionName;
                                }
                            }

                            controller.IsActive = true;
                            await _dbSet.AddAsync(controller);
                            addedCount++;
                        }
                    }

                    var changesCount = await _context.SaveChangesAsync();
                    _logger.LogInformation("[DynamicController] SaveChangesAsync 完成。新增: {AddCount} 筆, 更新/重啟: {UpdCount} 筆, EF 偵測異動總數: {Changes}。", addedCount, updatedCount, changesCount);

                    _cache.Remove(_cacheKey);
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
                var controllerType = Type.GetType(controller.ControllerTypeName);
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
                }
                else
                {
                    if (string.IsNullOrEmpty(controller.Version) && controller is ControllerMetadata metaDefault)
                    {
                        metaDefault.Version = "v1";
                    }
                }

                if (controllerType != null && controller is ControllerMetadata metaController)
                {
                    var controllerMenuAttr = controllerType.GetCustomAttribute<MenuAttribute>();

                    var methodInfo = controllerType.GetMethod(metaController.ActionName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
                    var actionMenuAttr = methodInfo?.GetCustomAttribute<MenuAttribute>();

                    string parentMenuName = null;
                    if (actionMenuAttr != null && !string.IsNullOrEmpty(actionMenuAttr.Parent))
                    {
                        parentMenuName = actionMenuAttr.Parent;
                    }
                    else if (string.Equals(metaController.ActionName, "Index", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(metaController.ActionName, "INDEX", StringComparison.OrdinalIgnoreCase))
                    {
                        parentMenuName = controllerMenuAttr?.Parent;
                    }
                    else
                    {
                        parentMenuName = controllerMenuAttr?.Name;
                    }

                    if (!string.IsNullOrEmpty(parentMenuName))
                    {
                        metaController.ParentMenuName = parentMenuName;
                    }

                    // 【修正點】優先取得 Action 的 Order，其次為 Controller 的 Order
                    if (actionMenuAttr != null)
                    {
                        metaController.DisplayOrder = actionMenuAttr.Order;
                    }
                    else if (controllerMenuAttr != null)
                    {
                        metaController.DisplayOrder = controllerMenuAttr.Order;
                    }

                    var actionDisplayAttr = methodInfo?.GetCustomAttribute<DisplayAttribute>();
                    string correctActionDisplayName = actionMenuAttr?.Name ?? actionDisplayAttr?.Name;

                    if (!string.IsNullOrEmpty(correctActionDisplayName))
                    {
                        metaController.DisplayName = correctActionDisplayName;
                    }
                    else if (metaController.DisplayName == metaController.ControllerName || string.IsNullOrEmpty(metaController.DisplayName))
                    {
                        metaController.DisplayName = metaController.ActionName;
                    }
                }
            }
        }

        private static string GetCleanTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return string.Empty;
            var commaIndex = typeName.IndexOf(',');
            return commaIndex > 0 ? typeName.Substring(0, commaIndex).Trim() : typeName.Trim();
        }

        public async Task UnregisterByModuleAsync(string moduleName)
        {
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
            }) ?? new List<T>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<T>> GetAllActiveAsync()
        {
            List<T> result = new List<T>();

            return await _cache.GetOrCreateAsync($"{_cacheKey}_AllActive", async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                result = await _dbSet.AsNoTracking().Where(x => x.IsActive).ToListAsync();

                return result;
            
            }) ?? new List<T>();
        }
    }
}