using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using SGSFramework.Core.Abstractions.Entities.Tenants;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Persistent.Abstractions.Partition;
using System.Linq.Expressions;
using System.Reflection;

namespace SGSFramework.Persistent.Configurations
{
    public static class DbConfigurationHelper
    {
        public static void ApplyCommonConfigs(ModelBuilder modelBuilder, string? schema, Type contextType, string? currentTenantId)
        {
            // 1. 設定全域預設 Schema (這影響沒有手動指定 ToTable 的實體)
            if (!string.IsNullOrEmpty(schema))
            {
                modelBuilder.HasDefaultSchema(schema);
            }

            // 2. 配置共用實體 (明確傳入 schema)
            modelBuilder.Entity<OutboxMessage>(entity => entity.ToTable("OutboxMessages", schema));

            //modelBuilder.Entity<SystemLog>(entity => entity.ToTable("SystemLogs"));

            // 3. 載入實體類別內部的配置 (IEntityTypeConfiguration)
            modelBuilder.ApplyConfigurationsFromAssembly(contextType.Assembly);

            // 4. 遍歷處理所有實體（自動處理 Index, Partition, Query Filters, Hierarchy）
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.ClrType == null) continue;

                // A. 額外 Attribute 配置 (Partition, Compression, AutoIndex)
                ApplyExtraConfigs(modelBuilder, entityType);

                // B. Global Query Filters (軟刪除與多租戶)
                ConfigureGlobalFilters(modelBuilder, entityType, currentTenantId);

                // C. 【補全】自動配置 HierarchyId 欄位的索引
                ConfigureHierarchyId(entityType);
            }


        }

        /// <summary>
        /// 自動為 HierarchyId 型別的屬性建立索引，優化樹狀查詢
        /// </summary>
        private static void ConfigureHierarchyId(IMutableEntityType entityType)
        {
            var hierarchyProperties = entityType.GetProperties()
                .Where(p => p.ClrType.FullName == "Microsoft.EntityFrameworkCore.HierarchyId");

            foreach (var property in hierarchyProperties)
            {
                // 為階層欄位建立索引
                entityType.AddIndex(property);
            }
        }


        // --- 以下為之前的 GlobalFilter 與 ExtraConfigs，保持邏輯一致 ---

        private static void ConfigureGlobalFilters(ModelBuilder modelBuilder, IMutableEntityType entityType, string? currentTenantId)
        {
            var clrType = entityType.ClrType;
            bool isSoftDelete = typeof(ISoftDeletable).IsAssignableFrom(clrType);
            bool isMultiTenant = typeof(ITenantEntity).IsAssignableFrom(clrType);
            if (!isSoftDelete && !isMultiTenant) return;

            var parameter = Expression.Parameter(clrType, "e");
            Expression? combinedExpression = null;

            if (isSoftDelete)
            {
                var isDeletedProp = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                combinedExpression = Expression.Equal(isDeletedProp, Expression.Constant(false));
            }

            if (isMultiTenant)
            {
                var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                var tenantExpression = Expression.Equal(tenantIdProp, Expression.Constant(currentTenantId, typeof(string)));
                combinedExpression = combinedExpression == null ? tenantExpression : Expression.AndAlso(combinedExpression, tenantExpression);
            }

            if (combinedExpression != null)
                modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(combinedExpression, parameter));
        }

        private static void ApplyExtraConfigs(ModelBuilder modelBuilder, IMutableEntityType entityType)
        {
            var clrType = entityType.ClrType;
            if (clrType == null) return;

            var partitionAttr = clrType.GetCustomAttribute<PartitionAttribute>();
            if (partitionAttr != null)
            {
                entityType.AddAnnotation("SES:PartitionScheme", partitionAttr.Scheme);
                entityType.AddAnnotation("SES:PartitionColumn", partitionAttr.Column);
            }

            foreach (var property in entityType.GetProperties())
            {
                var indexAttr = property.PropertyInfo?.GetCustomAttribute<AutoIndexAttribute>();
                if (indexAttr != null)
                {
                    var index = entityType.AddIndex(property);
                    index.IsUnique = indexAttr.IsUnique;
                    // 使用字串避免型別衝突
                    if (indexAttr.IncludeProperties?.Any() == true)
                        index.SetAnnotation("SqlServer:Include", indexAttr.IncludeProperties);
                }
            }
        }
    }
}