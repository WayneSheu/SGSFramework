namespace SGSFramework.Core.Abstractions.Entities.Controller;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;

/// <summary>
/// 動態選單實體 (Domain Entity)
/// </summary>
public class MenuItem
{
    public Guid Id { get; set; }
    public Guid? ControllerId { get; set; }
    public Guid? ParentId { get; set; }

    /// <summary>
    /// 節點識別 Key (例如: "ORG.LABORATORY.LIST")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 顯示名稱
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 圖示
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 前端路由路徑 (僅 MenuType.Page 必須填寫)
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// 前端對應 Component 組件路徑 (適用於 Vue/Blazor 動態載入)
    /// </summary>
    public string? Component { get; set; }

    /// <summary>
    /// 顯示排序
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// 節點類型 (Section, Group, Page)
    /// </summary>
    public MenuType Type { get; set; } = MenuType.Page;

    /// <summary>
    /// 模組識別碼 (區分主專案與 SES 外掛模組，如: "System", "ORG")
    /// </summary>
    public string ModuleName { get; set; } = "System";

    /// <summary>
    /// 關聯的權限鍵值
    /// </summary>
    public string? PermissionKey { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 是否在選單中顯示 (預留隱藏路由支援)
    /// </summary>
    public bool IsVisible { get; set; } = true;

    // 導覽屬性 (Navigation Property)
    public virtual MenuItem? Parent { get; set; }
    public virtual ICollection<MenuItem> Children { get; set; } = new List<MenuItem>();
}

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Route)
            .HasMaxLength(200);

        builder.Property(x => x.Component)
            .HasMaxLength(200);

        builder.Property(x => x.ModuleName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PermissionKey)
            .HasMaxLength(100);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // 樹狀結構 Self-referencing 關聯
        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // 複合索引優化高頻查詢
        builder.HasIndex(x => x.ParentId);
        builder.HasIndex(x => new { x.ModuleName, x.IsActive, x.DisplayOrder });
        builder.HasIndex(x => x.Key).IsUnique();
    }
}