using System.ComponentModel.DataAnnotations;


/// <summary>
/// 標準使用者配置請求資料模型 (策略類型已改由伺服器端 Option 控制，移除 Payload 內的 StrategyType)
/// </summary>
public sealed record UserProvisioningRequest
(
    [Required(ErrorMessage = "使用者帳號不得為空。")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "使用者帳號長度必須介於 3 到 50 個字元之間。")]
    string UserName,

    [Required(ErrorMessage = "電子郵件不得為空。")]
    [EmailAddress(ErrorMessage = "電子郵件格式不正確。")]
    string Email,

    [Required(ErrorMessage = "密碼不得為空。")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "密碼長度必須至少為 6 個字元。")]
    string? Password,

    string? DefaultRole,

    Guid? TenantLabId
);