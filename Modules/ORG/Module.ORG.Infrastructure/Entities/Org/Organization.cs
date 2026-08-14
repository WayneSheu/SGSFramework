using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Polly;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Entities.Hierarchical;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using System.ComponentModel.DataAnnotations;

namespace SGS.Modules.ORG.Infrastructure.Entities.Org
{
    /// <summary>
    /// 組織樹狀節點（使用 HierarchyId 支援無限層級與快速資料隔離查詢）
    /// </summary>
    public class Organization :  IHierarchicalEntity, IAuditable,ILedgerEntity, ISoftDeletable
    {
        public int Id { get; set; }           // 使用 int 確保索引效能

        public int? ParentId { get; set; }    // 維持 int 以便直接關聯

       
        public string Code { get; set; } // 組織代碼 (可選，視業務需求而定) 如果需要唯一性，可以在 DbContext 中配置唯一索引

        public string Name { get; set; }      // 節點名稱

        /// <summary>
        /// 跨模組多租戶繫結：若此組織節點為一個實體實驗室，可對應全系統統一的 LabId (Guid)
        /// </summary>
        public Guid? TenantLabId { get; set; }

        public string? Location  { get; set; } //所屬地區

        public string? Description { get; set; } // 節點描述

        [AutoIndex]  
        public string NodePath { get; set; }  // 存儲路徑 (如 "/1/5/10/")

        public int Level { get; set; }

        // 審計欄位
        [AuditIgnore]
        public string CreatedBy { get; set; }

        [AuditIgnore]
        [Editable(false)]
        public DateTimeOffset CreatedAtUtc { get; set; }

        [AuditIgnore]
        public string? UpdatedBy { get; set; }

        [AuditIgnore]
        public DateTimeOffset? UpdatedAtUtc { get; set; }
        
        //SoftDelete 欄位
        public bool IsDeleted { get; set; }

        [AuditIgnore]
        public string? DeletedBy { get; set; }

        [AuditIgnore]
        public DateTimeOffset? DeletedOnUtc { get; set; }

    }

    public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
    {
        public void Configure(EntityTypeBuilder<Organization> builder)
        {
            //顯式將 Organization 鎖定在 "org" Schema 下
            builder.ToTable("Organization", "org");

            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Id)
                   .ValueGeneratedOnAdd(); // Id 自動增量
            builder.Property(x => x.Name)
                   .IsRequired()
                   .HasMaxLength(50);
            builder.Property(x => x.Code).HasMaxLength(20);
            builder.Property(x => x.Description).HasMaxLength(200);

          
            // 確保 NodePath 映射到 MSSQL 的 hierarchyid 欄位，並建立索引以達最佳搜尋效能
            builder.Property(x => x.NodePath)
                   .IsRequired();
            // TenantLabId 建立索引，優化多租戶反查效能
            builder.HasIndex(x => x.TenantLabId)
                   .HasDatabaseName("IX_Organization_TenantLabId")
                   .HasFilter("[TenantLabId] IS NOT NULL");

        }
    }
}
