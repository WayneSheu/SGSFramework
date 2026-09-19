using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionGrants
{
    /// <summary>
    /// 權限元數據 DTO（用於資料庫投影傳輸）
    /// </summary>
    public sealed record PermissionMetadataDto(
        int Id,
        string ModuleName,
        string ModuleTitle,
        string ControllerName,
        string ControllerTitle,
        string ActionName,
        string ActionTitle,
        string PermissionKey,
        string PermissionTitle,
        string Description,
        int BitPosition
    );
}
