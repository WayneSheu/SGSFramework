namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionTree;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// 編輯單一權限項顯示名稱與說明之請求
/// </summary>
public sealed record UpdatePermissionMetadataRequest
{
    /// <summary>權限鍵值 (例如: SYSTEM.PERMISSION.READ)</summary>
    [Required]
    [MaxLength(128)]
    public string PermissionKey { get; init; } = string.Empty;

    /// <summary>所屬控制器名稱 (權限鍵可能被多個控制器共用)</summary>
    [Required]
    [MaxLength(128)]
    public string ControllerName { get; init; } = string.Empty;

    /// <summary>權限標題 (動作標題取自程式碼，不可修改)</summary>
    [Required]
    [MaxLength(128)]
    public string PermissionTitle { get; init; } = string.Empty;

    /// <summary>說明</summary>
    [MaxLength(256)]
    public string? Description { get; init; }
}
