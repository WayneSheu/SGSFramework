using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions.Enums
{
    /// <summary>
    /// 權限操作敏感度分級 (取代原有的具體行為 Mask，改為抽象級別)
    /// </summary>
    public enum ActionCategory
    {
        Basic = 1,       // 基本操作 (View, Export)
        Operational = 2, // 業務操作 (Create, Update)
        Critical = 3,    // 關鍵操作 (Delete, Approve)
        Administrative = 4 // 管理操作 (ManageUsers, SystemConfig)
    }
}
