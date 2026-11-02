namespace SGSFramework.Identity.DTOs.Users;

using System.ComponentModel.DataAnnotations;
using SGSFramework.Core.Abstractions.Entities.Identities;

/// <summary>
/// 更新使用者狀態請求 DTO
/// </summary>
public sealed class UpdateUserStatusRequest
{
    /// <summary>
    /// 目標使用者狀態
    /// </summary>
    [Required(ErrorMessage = "必須指定目標使用者狀態。")]
    public UserStatus Status { get; set; }

    /// <summary>
    /// 變更狀態原因/備註（選填，但對停用或鎖定操作建議記錄）
    /// </summary>
    [StringLength(250, ErrorMessage = "狀態變更原因長度不可超過 250 個字元。")]
    public string? Reason { get; set; }
}