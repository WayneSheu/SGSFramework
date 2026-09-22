namespace SGSFramework.AuthTokenBucket.DTOs.PermissionTree;

using System.Text.Json.Serialization;

/// <summary>
/// 權限操作/動作資料傳輸物件 (葉節點)
/// </summary>
public sealed class PermissionActionDto
{
    /// <summary>
    /// 權限唯一識別鍵值 (例如: ORG.LABORATORY.READ)
    /// </summary>
    [JsonPropertyName("permissionKey")]
    public string PermissionKey { get; set; } = string.Empty;

    /// <summary>
    /// 權限顯示名稱/標題 (例如: 實驗室管理)
    /// </summary>
    [JsonPropertyName("permissionTitle")]
    public string? PermissionTitle { get; set; }

    /// <summary>
    /// 對應 Controller Action 名稱
    /// </summary>
    [JsonPropertyName("actionName")]
    public string? ActionName { get; set; }

    /// <summary>
    /// 對應 Action 中文標題/顯示名稱
    /// </summary>
    [JsonPropertyName("actionTitle")]
    public string? ActionTitle { get; set; }

    /// <summary>
    /// 權限詳細描述說明
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Bitmask 位元位置索引
    /// </summary>
    [JsonPropertyName("bitPosition")]
    public long BitPosition { get; set; }
}