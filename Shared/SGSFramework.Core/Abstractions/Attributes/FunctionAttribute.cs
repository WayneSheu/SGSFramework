namespace SGSFramework.Core.Abstractions.Attributes;

using System;

/// <summary>
/// 用於標記 Method，明確指定 API 功能代碼、標題、圖示、排序、是否作為獨立選單 (MenuItem) 以及前端路由路徑。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class FunctionAttribute : Attribute
{
    /// <summary>
    /// 功能識別代碼 (例如: "GetLaboratories")
    /// </summary>
    public string FunctionName { get; }

    /// <summary>
    /// API Menu 的第二層標題名稱 (例如: "取得實驗室清單")
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 功能詳細說明或註解
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 選單圖示
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-link";

    /// <summary>
    /// 顯示排序 (越小越靠前)
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// 是否將此 API 標記為前端可視的選單項目 (MenuItem)
    /// </summary>
    public bool IsMenu { get; set; } = false;

    /// <summary>
    /// 前端對應的路由路徑 (例如: "/org/laboratories")，當 IsMenu 為 true 時指定
    /// </summary>
    public string? Path { get; set; }

    public FunctionAttribute(string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        FunctionName = functionName;
        Title = functionName;
    }

    public FunctionAttribute(string functionName, string title) : this(functionName)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title;
        }
    }
}