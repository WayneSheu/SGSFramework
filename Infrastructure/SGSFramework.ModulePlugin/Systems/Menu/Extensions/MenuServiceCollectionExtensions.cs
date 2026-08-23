using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.ModulePlugin.Systems.Menu.Strategies;
using SGSFramework.ModulePlugin.Systems.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Menu.Extensions
{
    /// <summary>
    /// 動態選單服務的 DI 註冊擴充方法
    /// </summary>
    public static class MenuServiceCollectionExtensions
    {
        public static IServiceCollection AddDynamicMenu(this IServiceCollection services)
        {

            // 註冊策略模式的各個策略實作
            services.AddSingleton<IMenuBuildingStrategy, SinglePhaseMenuBuildingStrategy>();
            services.AddSingleton<IMenuBuildingStrategy, TwoPhaseMenuBuildingStrategy>();

            services.AddScoped<IDynamicMenuService, DynamicMenuService>();


            return services;
        }
    }
}
