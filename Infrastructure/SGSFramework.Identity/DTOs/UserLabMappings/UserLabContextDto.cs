using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.UserLabMappings
{
    /// <summary>
    /// 使用者與實驗室關聯資料傳輸物件 (DTO)
    /// </summary>
    /// <param name="LabId"></param>
    /// <param name="LabName"></param>
    /// <param name="IsPrimary"></param>
    /// <param name="JobTitle"></param>
    /// <param name="IsExpired"></param>
    public record UserLabContextDto(
        Guid LabId,
        string LabName,
        bool IsPrimary,
        string? JobTitle,
        bool IsExpired
    );
}
