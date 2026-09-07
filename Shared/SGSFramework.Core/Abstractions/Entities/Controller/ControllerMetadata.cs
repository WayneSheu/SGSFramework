namespace SGSFramework.Core.Abstractions.Entities.Controller;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;

/// <summary>
/// API 控制器與 Action 掃描之中繼資料實體[cite: 9]
/// </summary>
public class ControllerMetadata : IControllerMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Version { get; set; } = "v1";
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleTitle { get; set; } = string.Empty;

    // === Controller 層級之中繼資料 ===
    public string ControllerTitle { get; set; } = string.Empty;
    public string ControllerName { get; set; } = string.Empty;
    public string ControllerTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Controller 層級之預設圖示 (作為 Group 節點圖示備援)
    /// </summary>
    public string? ControllerIcon { get; set; }

    /// <summary>
    /// Controller 層級之排序順序
    /// </summary>
    public int ControllerOrder { get; set; } = 0;

    // === Action 層級之中繼資料 ===
    public string ActionName { get; set; } = string.Empty;
    public string RouteTemplate { get; set; } = string.Empty;
    public string? Description { get; set; }

    // === 選單與渲染屬性 (相容 [Function] Attribute 掃描) ===
    /// <summary>
    /// Action 顯示名稱
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Action 頁面圖示
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Action 顯示排序
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// 父級選單名稱
    /// </summary>
    public string? ParentMenuName { get; set; }

    /// <summary>
    /// 是否作為選單節點 (由 [Function] Attribute 之 IsMenu 標記)
    /// </summary>
    public bool IsMenu { get; set; } = false;

    /// <summary>
    /// 前端路由路徑 (若為 null，則由 MenuSeedService 自動推導)
    /// </summary>
    public string? Path { get; set; }

    // === 位元遮罩解耦與 Attributes 中繼資料 ===
    /// <summary>
    /// 該 Action 在模組內對應的位元位置 (0 ~ 63)，用於 Bitmask 快速運算[cite: 9]
    /// </summary>
    public int? BitPosition { get; set; }

    /// <summary>
    /// 儲存該 Controller 或 Action 上掃描到的完整 Attributes 集合 (JSON 格式)[cite: 9]
    /// </summary>
    public string? AttributesJson { get; set; }

    // === 安全性與系統欄位 ===
    public string PermissionKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ControllerMetadataConfig : IEntityTypeConfiguration<ControllerMetadata>
{
    public void Configure(EntityTypeBuilder<ControllerMetadata> builder)
    {
        builder.ToTable("ControllerMetadatas");
        builder.HasKey(e => e.Id);

        builder.Property(x => x.ModuleName).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ModuleTitle).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ControllerTitle).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ControllerName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ControllerIcon).HasMaxLength(100);
        builder.Property(x => x.RouteTemplate).IsRequired().HasMaxLength(250);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Icon).HasMaxLength(100);
        builder.Property(x => x.Path).HasMaxLength(200);
        builder.Property(x => x.PermissionKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.AttributesJson).HasColumnType("nvarchar(max)");

        builder.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.IsMenu).HasDefaultValue(false);

        // 高頻選單查詢複合索引
        builder.HasIndex(x => x.IsMenu).HasDatabaseName("IX_Metadata_IsMenu");
        builder.HasIndex(x => new { x.IsMenu, x.ModuleName }).HasDatabaseName("IX_Metadata_IsMenu_ModuleName");
        builder.HasIndex(x => x.PermissionKey).HasDatabaseName("IX_Metadata_PermissionKey");
        builder.HasIndex(x => x.ModuleName).HasDatabaseName("IX_Metadata_ModuleName");

        builder.HasIndex(x => new { x.ModuleName, x.ControllerName, x.ActionName })
               .IsUnique()
               .HasDatabaseName("UX_Module_Controller_Action");
    }
}