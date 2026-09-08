using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Menus
{
    /// <summary>
    /// 選單樹解析與權限裁切策略抽象介面
    /// </summary>
    public interface IMenuResolutionStrategy
    {
        /// <summary>
        /// 策略唯一類型標示
        /// </summary>
        MenuStrategyType StrategyType { get; }

        /// <summary>
        /// 依據指定的權限集合與管理員身份，動態建構並執行雙向樹狀裁切 (Tree Pruning) 並產出 MenuSectionDto 集合
        /// </summary>
        Task<List<MenuSectionDto>> BuildMenuTreeAsync(
            IEnumerable<string> permissions,
            bool isAdmin,
            CancellationToken cancellationToken = default);
    }
}