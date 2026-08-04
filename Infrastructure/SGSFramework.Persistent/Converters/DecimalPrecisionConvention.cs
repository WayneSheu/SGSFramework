using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using SGSFramework.Core.Abstractions.Attributes;
using System;
using System.Linq;

namespace SGSFramework.Persistent.Converters
{
    /// <summary>
    /// 這個 Convention 用於在模型構建過程中自動應用 [DecimalPrecision] 屬性所指定的精度和規模設定。
    /// </summary>
    public class DecimalPrecisionConvention : IModelFinalizingConvention
    {
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                if (clrType == null) continue;

                // 尋找所有標註了 [DecimalPrecision] 的 decimal 屬性
                var properties = clrType.GetProperties()
                    .Where(p => p.PropertyType == typeof(decimal) && Attribute.IsDefined(p, typeof(DecimalPrecisionAttribute)));

                foreach (var propInfo in properties)
                {
                    var attr = (DecimalPrecisionAttribute)Attribute.GetCustomAttribute(propInfo, typeof(DecimalPrecisionAttribute))!;

                    // 1. 取得該屬性的內部定義物件 (IConventionProperty)
                    var property = entityType.FindProperty(propInfo.Name);
                    if (property == null) continue;

                    // 🟢 修正：直接操作底層元數據設定精度與規模，繞過所有 API 多載變更與具名引數錯誤
                    property.SetPrecision(attr.Precision, fromDataAnnotation: true);
                    property.SetScale(attr.Scale, fromDataAnnotation: true);

                    // 2. 透過屬性建構器動態掛載轉換器
                    var propertyBuilder = entityType.Builder.Property(propInfo);
                    propertyBuilder?.HasConversion(new RoundingConverter(attr.Scale, attr.Mode));
                }
            }
        }
    }
}