using SGSFramework.Core.Abstractions.Entities.Controller;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Menus;

/// <summary>
/// 選單樹構建策略介面 (Strategy Pattern)
/// </summary>
public interface IMenuBuildingStrategy
{
    /// <summary>
    /// 策略所對應的授權模式
    /// </summary>
    AuthorizationMode Mode { get; }

    /// <summary>
    /// 根據已授權的 ControllerMetadata 構建對應模式的選單區塊結構
    /// </summary>
    /// <param name="authorizedMetas">已通過權限檢核的控制器元資料清單</param>
    /// <returns>選單區塊集合</returns>
    IEnumerable<MenuSectionDto> BuildMenuSections(List<ControllerMetadata> authorizedMetas);
}

