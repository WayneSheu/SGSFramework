using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Menus
{
    /// <summary>
    /// 動態選單服務
    /// 負責從資料庫中的 ControllerMetadata 讀取 MenuAttribute 屬性，組裝成階層式選單結構。
    /// </summary>
    public interface IDynamicMenuService
    {
        Task<IEnumerable<MenuItemDto>> GetUserMenuAsync(IEnumerable<string> userPermissions);
    }
}
