// ==========================================
// 檔案路徑: src/SGSFramework/Presentation/SGSFramework.AuthTokenBucket/DTOs/PermissionDtos.cs
// ==========================================

using System.Collections.Generic;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionTree;

public class PermissionModuleDto
{
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleTitle { get; set; } = string.Empty;
    public List<PermissionFunctionDto> Functions { get; set; } = new();
}






