namespace SGSFramework.Core.Abstractions.Entities.Controller;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.ComponentModel;

/// <summary>
/// 動態選單實體 (Domain Entity)
/// </summary>`
[DisplayName("動態選單實體 (Domain Entity)")]
[Comment("動態選單實體 (Domain Entity)")]
public class MenuItem
{
    [DisplayName("識別主鍵")]
    [Comment("識別主鍵")]
    public Guid Id { get; set; }

    [DisplayName("控制器識別鍵")]
    [Comment("控制器識別鍵")]
    public Guid? ControllerId { get; set; }

    [DisplayName("父節點識別鍵")]
    [Comment("父節點識別鍵")]
    public Guid? ParentId { get; set; }

    /// <summary>
    /// 節點識別 Key (例如: "ORG.LABORATORY.LIST")
    /// </summary>
    [DisplayName("節點識別 Key (例如: ORG.LABORATORY.LIST)")]
    [Comment("節點識別 Key (例如: ORG.LABORATORY.LIST)")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 顯示名稱
    /// </summary>
    [DisplayName("顯示名稱")]
    [Comment("顯示名稱")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 圖示
    /// </summary>
    [DisplayName("圖示")]
    [Comment("圖示")]
    public string? Icon { get; set; }

    /// <summary>
    /// 前端路由路徑 (僅 MenuType.Page 必須填寫)
    /// </summary>
    [DisplayName("前端路由路徑 (僅 MenuType.Page 必須填寫)")]
    [Comment("前端路由路徑 (僅 MenuType.Page 必須填寫)")]
    public string? Route { get; set; }

    /// <summary>
    /// 前端對應 Component 組件路徑 (適用於 Vue/Blazor 動態載入)
    /// </summary>
    [DisplayName("前端對應 Component 組件路徑 (適用於 Vue/Blazor 動態載入)")]
    [Comment("前端對應 Component 組件路徑 (適用於 Vue/Blazor 動態載入)")]
    public string? Component { get; set; }

    /// <summary>
    /// 顯示排序
    /// </summary>
    [DisplayName("顯示排序")]
    [Comment("顯示排序")]
    public int DisplayOrder { get; set; }

    /// <summary>
    /// 節點類型 (Section, Group, Page)
    /// </summary>
    [DisplayName("節點類型 (Section, Group, Page)")]
    [Comment("節點類型 (Section, Group, Page)")]
    public MenuType Type { get; set; } = MenuType.Page;

    /// <summary>
    /// 模組識別碼 (區分主專案與 SES 外掛模組，如: "System", "ORG")
    /// </summary>
    [DisplayName("模組識別碼 (區分主專案與 SES 外掛模組，如: \"System\", \"ORG\")")]
    [Comment("模組識別碼 (區分主專案與 SES 外掛模組，如: \"System\", \"ORG\")")]
    public string ModuleName { get; set; } = "System";

    /// <summary>
    /// 關聯的權限鍵值
    /// </summary>
    [DisplayName("關聯的權限鍵值")]
    [Comment("關聯的權限鍵值")]
    public string? PermissionKey { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    [DisplayName("是否啟用")]
    [Comment("是否啟用")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 是否在選單中顯示 (預留隱藏路由支援)
    /// </summary>
    [DisplayName("是否在選單中顯示 (預留隱藏路由支援)")]
    [Comment("是否在選單中顯示 (預留隱藏路由支援)")]
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