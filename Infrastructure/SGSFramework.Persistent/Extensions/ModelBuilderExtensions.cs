using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SGSFramework.Persistent.Extensions
{

    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// 自動為實作 ISoftDeletable 的實體類型添加全局查詢過濾器，以過濾掉已軟刪除的記錄。
        /// </summary>
        /// <param name="modelBuilder"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static ModelBuilder ApplySoftDeleteFilters(this ModelBuilder modelBuilder)
        {
            if (modelBuilder == null) throw new ArgumentNullException(nameof(modelBuilder));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var propertyMethod = typeof(EF).GetMethod(nameof(EF.Property))?
                        .MakeGenericMethod(typeof(bool));

                    if (propertyMethod != null)
                    {
                        var isDeletedProperty = Expression.Call(
                            null,
                            propertyMethod,
                            parameter,
                            Expression.Constant(nameof(ISoftDeletable.IsDeleted))
                        );

                        var compare = Expression.Equal(
                            isDeletedProperty,
                            Expression.Constant(false)
                        );

                        var lambda = Expression.Lambda(compare, parameter);
                        modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);

                        // 生產級優化：自動為 IsDeleted 建立索引
                        modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(ISoftDeletable.IsDeleted));
                    }
                }
            }

            return modelBuilder;
        }
    }
}
