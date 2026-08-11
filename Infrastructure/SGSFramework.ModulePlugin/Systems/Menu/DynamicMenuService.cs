// ============================================================================
// Application / SGSFramework.ModulePlugin.Systems/Services/DynamicMenuService.cs
// ============================================================================
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Controllers.Services;
using SGSFramework.Core.DTOs;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGSFramework.ModulePlugin.Systems.Services;

public class DynamicMenuService : IDynamicMenuService
{
    private readonly IDynamicControllerRepository<ControllerMetadata> _controllerRepo;
    private readonly IReadOnlyDictionary<AuthorizationMode, IMenuBuildingStrategy> _strategies;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DynamicMenuService> _logger;

    public DynamicMenuService(
        IDynamicControllerRepository<ControllerMetadata> controllerRepo,
        IEnumerable<IMenuBuildingStrategy> strategies,
        IConfiguration configuration,
        ILogger<DynamicMenuService> logger)
    {
        _controllerRepo = controllerRepo ?? throw new ArgumentNullException(nameof(controllerRepo));
        ArgumentNullException.ThrowIfNull(strategies);
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 透過 DI 注入所有策略實作，並轉換成 Dictionary 供動態查詢
        _strategies = strategies.ToDictionary(s => s.Mode);
    }

    /// <summary>
    /// 依據系統組態動態選擇授權策略（單階段 vs 二階段）並構建選單
    /// </summary>
    public async Task<IEnumerable<MenuSectionDto>> GetUserMenuAsync(IEnumerable<string> userPermissions)
    {
        ArgumentNullException.ThrowIfNull(userPermissions);

        try
        {
            var permissionsSet = new HashSet<string>(userPermissions, StringComparer.OrdinalIgnoreCase);
            bool isSysAdmin = permissionsSet.Contains("sysadmin") || permissionsSet.Contains("*");

            var allMetas = await _controllerRepo.GetAllActiveAsync();

            var authorizedMetas = allMetas
                .Where(m => !string.IsNullOrEmpty(m.DisplayName) &&
                           (isSysAdmin || string.IsNullOrEmpty(m.PermissionKey) || permissionsSet.Contains(m.PermissionKey)))
                .DistinctBy(m => new { m.ControllerName, m.ActionName })
                .OrderBy(m => m.DisplayOrder)
                .ToList();

            // 從 appsettings.json 讀取授權模式，預設為 TwoPhase
            var modeString = _configuration["AuthorizationSettings:Mode"];
            if (!Enum.TryParse<AuthorizationMode>(modeString, ignoreCase: true, out var currentMode))
            {
                currentMode = AuthorizationMode.TwoPhase;
            }

            // 策略模式選擇：動態匹配對應的策略實作
            if (!_strategies.TryGetValue(currentMode, out var selectedStrategy))
            {
                _logger.LogWarning("未找到模式 {Mode} 對應的選單策略，切換至預設 TwoPhase 策略。", currentMode);
                selectedStrategy = _strategies[AuthorizationMode.TwoPhase];
            }

            _logger.LogInformation("當前選單構建使用策略：{StrategyType} (AuthorizationMode: {Mode})",
                selectedStrategy.GetType().Name, currentMode);

            return selectedStrategy.BuildMenuSections(authorizedMetas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根據權限動態生成選單時發生異常。");
            return Enumerable.Empty<MenuSectionDto>();
        }
    }
}