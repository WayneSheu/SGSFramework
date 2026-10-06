using SGSFramework.SPAModulePlugin.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SPAModulePlugin.Domain.ValueObjects
{
    /// <summary>
    /// SPA 外掛模組中繼資料描述物件
    /// </summary>
    public sealed record SPAModuleManifest
    {
        public required string ModuleId { get; init; }
        public required string DisplayName { get; init; }
        public required SPAFrameworkType FrameworkType { get; init; }
        public required string EntryPoint { get; init; }      // Vue 的 ES Module 檔名，或 Blazor 的主 Assembly (.dll)
        public required string RoutePath { get; init; }       // 前端掛載動態路由
        public required string RequiredPermission { get; init; }
        public required string FileHash { get; init; }        // SHA-256 安全校驗雜湊
        public IReadOnlyList<string> DependentAssets { get; init; } = Array.Empty<string>();
    }
}
