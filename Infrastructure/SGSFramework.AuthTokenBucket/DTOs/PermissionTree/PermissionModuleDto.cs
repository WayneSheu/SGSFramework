namespace SGSFramework.AuthTokenBucket.DTOs.PermissionTree;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// 系統模組資料傳輸物件 (根層級節點)
/// </summary>
public sealed class PermissionModuleDto
{
    /// <summary>
    /// 模組系統識別名稱 (例如: SGS.Modules.ORG)
    /// </summary>
    [JsonPropertyName("moduleName")]
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>
    /// 模組中文標題 (例如: 組織管理)
    /// </summary>
    [JsonPropertyName("moduleTitle")]
    public string? ModuleTitle { get; set; }

    /// <summary>
    /// 模組詳細描述說明
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// 模組下屬之功能/控制器清單
    /// </summary>
    [JsonPropertyName("functions")]
    public List<PermissionFunctionDto> Functions { get; set; } = new();
}