using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Attributes
{
    /// <summary>
    /// 標記控制器的屬性，用於定義菜單名稱、圖標、排序順序和父菜單。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class MenuAttribute : Attribute
    {
        public string Name { get; }
        public string Icon { get; }
        public int Order { get; }
        public string? Parent { get; }
        public bool IsVisible { get; set; } = true; //控制器是否顯示在選單

        public MenuAttribute(string name, string icon = "fa-solid fa-list", int order = 100, string? parent = null  )
        {
            Name = name;
            Icon = icon;
            Order = order;
            Parent = parent;
        }
    }
}
