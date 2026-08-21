using System;
using System.Text.RegularExpressions;

namespace SGSFramework.Core.Abstractions.Attributes;

/// <summary>
/// 用於標記 Assembly、Controller 或 Method，明確其業務模組代碼、API Menu 頂層 Section 標題、圖示與排序。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class ModuleAttribute : Attribute
{
    private static readonly Regex CodeValidator = new(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

    /// <summary>
    /// 模組識別代碼 (保留原始大小寫，可用於 DB Schema / Table Prefix)
    /// </summary>
    public string ModuleCode { get; }

    /// <summary>
    /// 模組顯示名稱 (例如: "PhysLIMS")
    /// </summary>
    public string ModuleName { get; }

    /// <summary>
    /// API Menu 的 Section 區塊標題名稱 (例如: "物理實驗室管理模組")
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 選單詳細說明 (例如: "包含物理性檢測、儀器排程與測試報告相關功能")
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// API Menu Section 的區塊圖示 (例如: "fa-solid fa-cubes")
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-layer-group";

    /// <summary>
    /// API Menu Section 的顯示排序 (越小越靠前)
    /// </summary>
    public int Order { get; set; } = 0;

    public ModuleAttribute(string moduleCode, string moduleName, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(moduleCode))
            throw new ArgumentException("Module Code cannot be empty.", nameof(moduleCode));

        if (!CodeValidator.IsMatch(moduleCode))
            throw new ArgumentException($"Module Code '{moduleCode}' contains invalid characters. Only alphanumeric and underscore are allowed.", nameof(moduleCode));

        ModuleCode = moduleCode;
        ModuleName = string.IsNullOrWhiteSpace(moduleName) ? moduleCode : moduleName;
        Title = string.IsNullOrWhiteSpace(title) ? ModuleName : title;
    }
}