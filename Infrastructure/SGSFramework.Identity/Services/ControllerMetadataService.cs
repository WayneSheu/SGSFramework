using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Services
{
    /// <summary>
    /// 系統控制器中繼資料服務實作（支援權限綁定樹狀與平坦清單查詢）
    /// </summary>
    public class ControllerMetadataService(
        DbContext dbContext,
        ILogger<ControllerMetadataService> logger) : IControllerMetadataService
    {
        private readonly DbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly ILogger<ControllerMetadataService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<IEnumerable<ControllerMetadataDto>> GetAllControllerMetadatasAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbContext.Set<ControllerMetadata>() //[cite: 10]
                    .AsNoTracking()
                    .Where(m => m.IsActive) //[cite: 10]
                    .OrderBy(m => m.DisplayOrder) //[cite: 10]
                    .Select(m => new ControllerMetadataDto
                    {
                        Id = m.Id, //[cite: 10]
                        Version = m.Version, //[cite: 10]
                        ModuleName = m.ModuleName, //[cite: 10]
                        ModuleTitle = m.ModuleTitle, //[cite: 10]
                        ControllerTitle = m.ControllerTitle, //[cite: 10]
                        ControllerName = m.ControllerName, //[cite: 10]
                        ControllerTypeName = m.ControllerTypeName, //[cite: 10]
                        ActionName = m.ActionName, //[cite: 10]
                        RouteTemplate = m.RouteTemplate, //[cite: 10]
                        Description = m.Description, //[cite: 10]
                        DisplayName = m.DisplayName, //[cite: 10]
                        Icon = m.Icon, //[cite: 10]
                        DisplayOrder = m.DisplayOrder, //[cite: 10]
                        ParentMenuName = m.ParentMenuName, //[cite: 10]
                        PermissionKey = m.PermissionKey, //[cite: 10]
                        IsActive = m.IsActive //[cite: 10]
                    })
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得系統控制器中繼資料清單時發生未預期異常。");
                throw;
            }
        }

        public async Task<IEnumerable<ModulePermissionGroupDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default)
        {
            var flatList = await GetAllControllerMetadatasAsync(cancellationToken);

            // 於記憶體中進行階層群組轉換 (ModuleName -> ControllerName -> Actions)
            var tree = flatList
                .GroupBy(m => new { m.ModuleName, m.ModuleTitle })
                .Select(moduleGroup => new ModulePermissionGroupDto
                {
                    ModuleName = moduleGroup.Key.ModuleName,
                    ModuleTitle = moduleGroup.Key.ModuleTitle,
                    Controllers = moduleGroup
                        .GroupBy(c => new { c.ControllerName, c.ControllerTitle, c.Icon, c.DisplayOrder })
                        .Select(controllerGroup => new ControllerPermissionGroupDto
                        {
                            ControllerName = controllerGroup.Key.ControllerName,
                            ControllerTitle = controllerGroup.Key.ControllerTitle,
                            Icon = controllerGroup.Key.Icon,
                            DisplayOrder = controllerGroup.Key.DisplayOrder,
                            Actions = controllerGroup.OrderBy(a => a.DisplayOrder).ToList()
                        })
                        .OrderBy(c => c.DisplayOrder)
                        .ToList()
                })
                .ToList();

            return tree;
        }
    }
}
