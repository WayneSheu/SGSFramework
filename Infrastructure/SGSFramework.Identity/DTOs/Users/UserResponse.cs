// ==========================================
// 檔案路徑: src/Presentation/SGSFramework.Identity/DTOs/Users/UserResponse.cs
// ==========================================
namespace SGSFramework.Identity.DTOs.Users;

using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Extensions;
using System;

public class UserResponse
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public bool IsSystemAdmin { get; set; } = false;

    /// <summary>
    /// 使用者狀態列舉
    /// </summary>
    public UserStatus Status { get; set; } = UserStatus.Active;
    /// <summary>
    /// 帳號狀態中文描述 (例如：啟用中、已停用等)
    /// </summary>
    public string StatusText => Status.GetDisplayName();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 員工編號
    /// </summary>
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>
    /// 使用者真實姓名
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    public Guid? PrimaryLabId { get; set; }
    public string? PrimaryLabName { get; set; }
}