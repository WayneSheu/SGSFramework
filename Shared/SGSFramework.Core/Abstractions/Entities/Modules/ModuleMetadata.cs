using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SGSFramework.Core.Abstractions.Entities.Modules
{
    public class ModuleMetadata : IModuleEntity
    {
        public Guid Id { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string AssemblyPath { get; set; } = string.Empty; // 持久化路徑
        public bool IsActive { get; set; } = true;              // 控制是否載入
        public DateTime LastLoadedAt { get; set; }
        public string Checksum { get; set; } = string.Empty;    // 用於版本校驗
    }

    public class ModuleMetadataConfig : IEntityTypeConfiguration<ModuleMetadata>
    {
        public void Configure(EntityTypeBuilder<ModuleMetadata> builder)
        {
            // 設定主鍵
            builder.HasKey(e => e.Id);

            // 模組名稱作為核心索引，確保不可重複
            builder.HasIndex(e => e.ModuleName).IsUnique();

            // 屬性精確設定
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