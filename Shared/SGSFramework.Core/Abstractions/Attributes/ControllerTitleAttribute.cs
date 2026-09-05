using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Attributes;

/// <summary>
/// 用於標記 Controller 類別，明確指定 API Menu 的第一層標題名稱、圖示、排序與詳細描述。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class ControllerTitleAttribute : Attribute
{
    /// <summary>
    /// 第一層標題名稱 (例如: "實驗室管理")
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 選單圖示 (例如: "fa-solid fa-flask")
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-folder";

    /// <summary>
    /// 顯示排序 (越小越靠前)
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// 功能說明或註解 (例如: "維護組織樹狀結構下的各級實驗室資訊")
    /// </summary>
    public string? Description { get; set; }

    public ControllerTitleAttribute(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Controller Title cannot be empty.", nameof(title));
        Title = title;
    }
}