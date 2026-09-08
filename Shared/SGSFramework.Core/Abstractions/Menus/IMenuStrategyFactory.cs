using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Menus
{
    /// <summary>
    /// 選單解析策略工廠介面
    /// </summary>
    public interface IMenuStrategyFactory
    {
        /// <summary>
        /// 依策略類型獲取對應之選單解析策略
        /// </summary>
        IMenuResolutionStrategy GetStrategy(MenuStrategyType strategyType = MenuStrategyType.DatabaseDriven);
    }
}
