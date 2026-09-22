namespace SGSFramework.Core.Abstractions.Entities.Controller;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.Modules;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// API 控制器與 Action 中繼資料實體
/// </summary>
[Comment("API 控制器與 Action 中繼資料實體")]
public class ControllerMetadata : IControllerMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// API 版本號 (例如: v1, v2, etc.)
    /// </summary>
    [DisplayName("API版本號")]
    [Comment("API 版本號 (例如: v1, v2, etc.)")]
    public string Version { get; set; } = "v1";

    [DisplayName("模組名稱")]
    [Comment("取自AssemblyInfo Attribute 的ModuleName 屬性。")]
    public string ModuleName { get; set; } = string.Empty;

    [DisplayName("模組標題")]
    [Comment("取自AssemblyInfo Attribute 的ModulTitle。")]
    public string ModuleTitle { get; set; } = string.Empty;

    // === Controller 層級之中繼資料 ===
    [DisplayName("控制器標題")]
    [Comment("取自ControllerTitle Atteribute 的ControllerTitle。")]
    public string ControllerTitle { get; set; } = string.Empty;

    [DisplayName("控制器名稱")]
    [Comment("取自ControllerName Atteribute 的ControllerName。")]
    public string ControllerName { get; set; } = string.Empty;

    /// <summary>
    /// Controller 層級之類型名稱 (例如: Controller, Action, etc.)
    /// </summary>
    [DisplayName("控制器類別名稱")]
    [Comment("取自ControllerTypeName Atteribute 的ControllerTypeName。")]
    public string ControllerTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Controller 層級之預設圖示 (作為 Group 節點圖示備援)
    /// </summary>
    [DisplayName("控制器圖示")]
    [Comment("取自ControllerIcon Atteribute 的ControllerIcon。")]
    public string? ControllerIcon { get; set; }

    /// <summary>
    /// Controller 層級之排序順序
    /// </summary>
    [DisplayName("控制器排序")]
    [Comment("取自ControllerOrder Atteribute 的ControllerOrder。")]
    public int ControllerOrder { get; set; } = 0;

    // === Action 層級之中繼資料 ===
    [DisplayName("動作名稱")]
    [Comment("取自ActionName Atteribute 的ActionName。")]
    public string ActionName { get; set; } = string.Empty;

    [DisplayName("API路由模板")]
    [Comment("供前端 Axios / HttpClient 進行 HTTP 請求。")]
    public string RouteTemplate { get; set; } = string.Empty;

    [DisplayName("動作說明")]
    [Comment("動作說明 (供前端 Axios / HttpClient 顯示)。")]
    public string? Description { get; set; }

    // === 選單與渲染屬性 (相容 [Function] Attribute 掃描) ===
    /// <summary>
    /// Action 顯示標題
    /// </summary>
    [DisplayName("動作標題")]
    [Comment("動作標題 (供前端 Axios / HttpClient 顯示)。")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Action 頁面圖示
    /// </summary>
    [DisplayName("動作圖示")]
    [Comment("動作圖示 (供前端 Axios / HttpClient 顯示)。")]
    public string? Icon { get; set; }

    /// <summary>
    /// Action 顯示排序
    /// </summary>
    [DisplayName("動作排序")]
    [Comment("動作排序 (供前端 Axios / HttpClient 顯示)。")]
    public int DisplayOrder { get; set; }

    /// <summary>
    /// 父級選單名稱
    /// </summary>
    [DisplayName("父級選單名稱")]
    [Comment("父級選單名稱 (供前端 Axios / HttpClient 顯示)。")]
    public string? ParentMenuName { get; set; }

    /// <summary>
    /// 是否作為選單節點 (由 [Function] Attribute 之 IsMenu 標記)
    /// </summary>
    [DisplayName("是否為選單")]
    [Comment("是否為選單 (由 [Function] Attribute 之 IsMenu 標記)。")]
    public bool IsMenu { get; set; } = false;

    /// <summary>
    /// 前端路由路徑 (若為 null，則由 MenuSeedService 自動推導)
    /// </summary>
    [DisplayName("前端路由路徑")]
    [Comment("供前端路由系統進行頁面跳轉與選單點擊導航。")]
    public string? Path { get; set; }

    // === 位元遮罩解耦與 Attributes 中繼資料 ===
    /// <summary>
    /// 該 Action 在模組內對應的位元位置 (0 ~ 63)，用於 Bitmask 快速運算
    [DisplayName("位元遮罩解")]
    [Comment("該 Action 在模組內對應的位元位置 (0 ~ 63)，用於 Bitmask 快速運算。")]
    public int? BitPosition { get; set; }

    /// <summary>
    /// 儲存該 Controller 或 Action 上掃描到的完整 Attributes 集合 (JSON 格式)
    /// </summary>
    [DisplayName("Attributes 集合")]
    [Comment(" Controller 或 Action 完整 Attributes 集合 (JSON 格式)")]
    public string? AttributesJson { get; set; }

    // === 安全性與系統欄位 ===
    [DisplayName("權限Key")]
    [Comment("權限Key (供前端 Axios / HttpClient 顯示)。")]
    public string PermissionKey { get; set; } = string.Empty;

    [DisplayName("是否啟用")]
    [Comment("標記是否啟用")]
    public bool IsActive { get; set; } = true;

    [DisplayName("建立日期")]
    [Comment("建立日期")]
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