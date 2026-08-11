using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.ModulePlugin.Systems.Services;

namespace SGSFramework.ModulePlugin.Systems.Menu.Extensions
{
    /// <summary>
    /// 動態選單服務的 DI 註冊擴充方法
    /// </summary>
    public static class MenuServiceCollectionExtensions
    {
        public static IServiceCollection AddDynamicMenu(this IServiceCollection services)
        {
            services.AddScoped<IDynamicMenuService, DynamicMenuService>();
            return services;
        }
    }
}
