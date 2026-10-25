using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Entities.Modules
{
    /// <summary>
    /// 模組元資料實體，包含模組的基本資訊與狀態
    /// </summary>
    [DisplayName("模組元資料")]
    [Comment("模組元資料實體，包含模組的基本資訊與狀態")]
    public class ModuleMetadata : IModuleEntity
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]   
        public Guid Id { get; set; }
       
        [DisplayName("模組標題")]
        [Comment("模組標題")]   
        public string ModuleTitle { get; set; } = string.Empty;
        
        [DisplayName("模組名稱")]
        [Comment("模組名稱")]   
        public string ModuleName { get; set; } = string.Empty;
        
        [DisplayName("版本")]
        [Comment("版本")]   
        public string Version { get; set; } = "1.0.0";
       
        [DisplayName("組件路徑")]
        [Comment("組件路徑")]   
        public string AssemblyPath { get; set; } = string.Empty; // 持久化路徑
        
        [DisplayName("是否啟用")]
        [Comment("是否啟用")]   
        public bool IsActive { get; set; } = true;              // 控制是否載入
        
        [DisplayName("最後載入時間")]
        [Comment("最後載入時間")]   
        public DateTime LastLoadedAt { get; set; }
        
        [DisplayName("校驗和")]
        [Comment("校驗和")]    
        public string Checksum { get; set; } = string.Empty;    // 用於版本校驗
    }

    public class ModuleMetadataConfig : IEntityTypeConfiguration<ModuleMetadata>
    {
        public void Configure(EntityTypeBuilder<ModuleMetadata> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("ModuleMetadatas", "core");

            // 設定主鍵
            builder.HasKey(e => e.Id);

            // 模組名稱作為核心索引，確保不可重複
            builder.HasIndex(e => e.ModuleName).IsUnique();

            // 屬性精確設定
            builder.Property(e => e.ModuleTitle)
           .IsRequired()
           .HasMaxLength(100);

            builder.Property(e => e.ModuleName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Version)
                .HasMaxLength(20)
                .HasDefaultValue("1.0.0");

            builder.Property(e => e.AssemblyPath)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(e => e.Checksum)
                .HasMaxLength(64); // 若使用 SHA-256，長度建議設為 64

            builder.Property(e => e.LastLoadedAt)
                .IsRequired();
           
        }
    }
}