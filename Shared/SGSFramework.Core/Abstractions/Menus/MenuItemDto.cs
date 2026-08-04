using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Menus
{
    public class MenuItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string? Parent { get; set; }
        public int Order { get; set; }
        public List<MenuItemDto> Children { get; set; } = new();
    }
}
