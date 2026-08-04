namespace SGSFramework.Core.Abstractions.Entities.Hierarchical
{
    /// <summary>
    /// 樹狀階層實體統一介面
    /// </summary>
    public interface IHierarchicalEntity
    {
        int Id { get; } // 使用 int 確保索引效能
        int? ParentId { get; set; }    // 維持 int 以便直接關聯
        string NodePath { get; set; }  // 存儲路徑 (如 "/1/5/10/")
        int Level { get; set; }
    }
}
