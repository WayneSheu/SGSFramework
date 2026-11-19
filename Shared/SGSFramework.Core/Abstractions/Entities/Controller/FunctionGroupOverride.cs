using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Entities.Controller
{
    /// <summary>
    /// 功能分組層級：模組 (Level 1) / Controller (Level 2)
    /// </summary>
    public enum FunctionGroupLevel
    {
        Module = 1,
        Controller = 2
    }

    /// <summary>
    /// 「系統功能管理」模組 / Controller 層級的顯示覆寫（名稱、圖示、排序與顯示用父層）。
    /// 僅影響畫面呈現；ControllerMetadata 的 ModuleName / ControllerName、權限鍵與位元遮罩一律不變，
    /// 啟動同步也不會讀寫此表，因此管理者的調整不會被程式碼 Attribute 覆蓋。
    /// </summary>
    [Comment("系統功能管理：模組 / Controller 層級的顯示覆寫（名稱、圖示、排序、顯示用父層）")]
    public class FunctionGroupOverride
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("分組層級")]
        [Comment("分組層級：1 = 模組、2 = Controller")]
        public FunctionGroupLevel Level { get; set; }

        /// <summary>來源模組名稱（ControllerMetadata.ModuleName）</summary>
        [DisplayName("模組名稱")]
        [Comment("來源模組名稱（ControllerMetadata.ModuleName）")]
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>來源 Controller 名稱（ControllerMetadata.ControllerName）；模組層級為空字串</summary>
        [DisplayName("控制器名稱")]
        [Comment("來源 Controller 名稱；模組層級為空字串")]
        public string ControllerName { get; set; } = string.Empty;

        [DisplayName("顯示名稱")]
        [Comment("自訂顯示名稱；為 null 時沿用程式碼標題")]
        public string? Title { get; set; }

        [DisplayName("圖示")]
        [Comment("自訂圖示 (Font Awesome class)；僅 Controller 層級使用")]
        public string? Icon { get; set; }

        [DisplayName("顯示排序")]
        [Comment("自訂排序；為 null 時沿用預設排序")]
        public int? DisplayOrder { get; set; }

        /// <summary>Controller 層級：改掛顯示於哪個模組之下（為 null 時顯示於原模組）</summary>
        [DisplayName("顯示用父模組")]
        [Comment("Controller 層級：改掛顯示於哪個模組之下；為 null 時顯示於原模組")]
        public string? TargetModuleName { get; set; }

        [DisplayName("最後異動者")]
        [Comment("最後異動者")]
        public string? UpdatedBy { get; set; }

        [DisplayName("最後異動時間")]
        [Comment("最後異動時間 (UTC)")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class FunctionGroupOverrideConfiguration : IEntityTypeConfiguration<FunctionGroupOverride>
    {
        public void Configure(EntityTypeBuilder<FunctionGroupOverride> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("FunctionGroupOverrides", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Level).HasConversion<int>();
            builder.Property(x => x.ModuleName).HasMaxLength(50).IsRequired();
            builder.Property(x => x.ControllerName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Title).HasMaxLength(100);
            builder.Property(x => x.Icon).HasMaxLength(100);
            builder.Property(x => x.TargetModuleName).HasMaxLength(50);
            builder.Property(x => x.UpdatedBy).HasMaxLength(256);
            builder.HasIndex(x => new { x.Level, x.ModuleName, x.ControllerName })
                   .IsUnique()
                   .HasDatabaseName("UX_FunctionGroupOverride_Level_Module_Controller");
        }
    }
}
