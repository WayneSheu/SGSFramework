using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Core.Abstractions.Entities.Identities;

/// <summary>
/// 使用者帳號狀態
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// 待啟用 / 待審核
    /// </summary>
    [Display(Name = "待啟用")]
    Pending = 0,

    /// <summary>
    /// 正常啟用中
    /// </summary>
    [Display(Name = "啟用中")]
    Active = 1,

    /// <summary>
    /// 已停用 / 暫停使用（如離職、留停）
    /// </summary>
    [Display(Name = "已停用")]
    Disabled = 2,

    /// <summary>
    /// 安全性鎖定（如嘗試登入失敗過多）
    /// </summary>
    [Display(Name = "已鎖定")]
    Locked = 3
}