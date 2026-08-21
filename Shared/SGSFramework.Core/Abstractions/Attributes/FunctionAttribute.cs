using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Attributes;

/// <summary>
/// 用於標記 Method，明確指定 API Menu 的第二層標題名稱、功能代碼、圖示、排序與詳細描述。
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
    /// 功能詳細說明或註解 (例如: "查詢使用者權限內可看見的實驗室分頁列表")
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 選單圖示 (例如: "fa-solid fa-list")
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-link";

    /// <summary>
    /// 顯示排序 (越小越靠前)
    /// </summary>
    public int Order { get; set; } = 0;

    public FunctionAttribute(string functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("Function Name cannot be empty.", nameof(functionName));

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