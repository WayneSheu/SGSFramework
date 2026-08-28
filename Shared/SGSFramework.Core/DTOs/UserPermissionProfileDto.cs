using SGSFramework.Core.Abstractions.Menus;
using System;
using System.Collections.Generic;

namespace SGSFramework.Core.DTOs;


/// <summary>
/// 使用者執行期權限配置傳輸物件 (DTO)
/// 用於封裝使用者在特定實驗室上下文中的完整權限與選單狀態
/// </summary>
public sealed class UserPermissionProfileDto
{
    /// <summary>
    /// 使用者識別碼
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// 當前作用中的實驗室識別碼 (LabId)
    /// </summary>
    public required Guid TenantLabId { get; init; }

    /// <summary>`
    /// 當前作用中的實驗室名稱
    /// </summary>
    public string LabName { get; init; } = string.Empty;

    /// <summary>
    /// 該實驗室環境下所擁有的扁平化權限鍵值集合 (供前端比對按鈕級別權限)
    /// </summary>
    public IEnumerable<string> Permissions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 依據該實驗室權限過濾後生成的動態選單樹
    /// </summary>
    public IEnumerable<MenuSectionDto> Menus { get; init; } = Array.Empty<MenuSectionDto>();

    /// <summary>
    /// 可存取的實驗室清單
    /// </summary>
    public List<AccessibleLabDto> AccessibleLabs { get; init; } = new();

}