using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using SGSFramework.Core.Abstractions.Attributes;

namespace SGSFramework.Persistent.Converters
{
    /// <summary>
    /// 自動建立索引的慣例（Convention）。
    /// </summary>
    /// <remarks>
    /// 這個慣例會在模型最終化（model finalization）階段掃描模型中所有實體型別的 CLR 屬性，
    /// 對標記有 `AutoIndexAttribute` 的屬性自動建立索引（等同於在對應的實體型別上呼叫 `HasIndex(...)`）。
    ///
    /// 使用情境：
    /// - 當專案內有多個實體屬性需要一致地建立索引時，可透過在屬性上加上 `AutoIndexAttribute`，
    ///   並註冊此慣例，避免在各處手動維護索引設定。
    ///
    /// 注意事項：
    /// - 只掃描 CLR 屬性；若實體類型沒有對應 CLR 類型或屬性不存在，會跳過該項目。
    /// - 預設只會為單一屬性建立索引；複合索引（multiple-column index）需額外支援或使用其他慣例/手動設定。
    /// - 此慣例在模型建置/最終化期間執行，不適合在執行階段（runtime）頻繁呼叫。
    /// - 若資料庫或另一個慣例已建立相同索引，請注意可能產生重複或衝突，需於遷移/執行時檢查。
    ///
    /// 註冊方式（範例）：
    /// - 請將此慣例加入 Entity Framework Core 的慣例集合（ConventionSet）或在建立模型的過程中註冊，
    ///   以確保在模型最終化階段能被執行。
    ///
    /// 範例：
    /// - 在實體屬性上宣告： 
    ///   `public class MyEntity { [AutoIndex] public string Code { get; set; } }`
    /// - 註冊慣例後，模型建置完成時會自動為 `Code` 屬性新增索引。
    /// </remarks>
    public class AutoIndexConvention : IModelFinalizingConvention
    {
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            // 遍歷模型中所有的實體類型
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
            {
                // 獲取該實體對應的 CLR 類型（例如 SystemLog）
                var clrType = entityType.ClrType;//對應到資料庫表的 CLR 類型(EntityType 對應到資料庫表，ClrType 對應到實體類型)
                if (clrType == null) continue;

                // 掃描所有屬性，尋找帶有 [AutoIndex] 的屬性
                var propertiesWithAttribute = clrType.GetProperties()
                    .Where(p => Attribute.IsDefined(p, typeof(AutoIndexAttribute)));

                foreach (var propertyInfo in propertiesWithAttribute)
                {
                    // 自動為該屬性建立索引
                    entityType.Builder.HasIndex(new[] { propertyInfo.Name });
                }
            }
        }
    }
}
