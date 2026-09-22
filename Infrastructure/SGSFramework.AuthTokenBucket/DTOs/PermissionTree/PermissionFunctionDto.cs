namespace SGSFramework.AuthTokenBucket.DTOs.PermissionTree;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// 功能/控制器資料傳輸物件 (功能層節點)
/// </summary>
public sealed class PermissionFunctionDto
{
    /// <summary>
    /// 控制器/功能名稱 (例如: LaboratoryController)
    /// </summary>
    [JsonPropertyName("functionName")]
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>
    /// 功能/控制器中文標題 (例如: 實驗室管理)
    /// </summary>
    [JsonPropertyName("functionTitle")]
    public string? FunctionTitle { get; set; }

    /// <summary>
    /// 功能詳細描述說明
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// 進入該功能/選單的主讀取權限 (通常為結尾 _READ 之權限)
    /// </summary>
    [JsonPropertyName("readPermission")]
    public PermissionActionDto? ReadPermission { get; set; }

    /// <summary>
    /// 該功能下屬之子操作權限清單 (如 CREATE, UPDATE, DELETE 等，不包含 ReadPermission)
    /// </summary>
    [JsonPropertyName("actionPermissions")]
    public List<PermissionActionDto> ActionPermissions { get; set; } = new();
}