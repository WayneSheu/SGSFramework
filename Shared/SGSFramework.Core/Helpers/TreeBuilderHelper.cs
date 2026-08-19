using SGSFramework.Core.Abstractions.Entities.Hierarchical;
using SGSFramework.Core.Abstractions.TreeNodes;

namespace SGSFramework.Core.Helpers;

/// <summary>
/// 企業級高效能泛型樹狀結構組裝工具類別 (O(N) 哈希映射演算法)
/// </summary>
public static class TreeBuilderHelper
{
    /// <summary>
    /// 通用標準泛型樹狀結構組裝 - O(N) 時間複雜度
    /// </summary>
    /// <typeparam name="TKey">主鍵與外鍵型態 (如 int, Guid, string)</typeparam>
    /// <typeparam name="TNode">實現 ITreeNode 介面之節點類別</typeparam>
    /// <param name="items">來源平舖清單 (Flat List)</param>
    /// <param name="rootParentId">根節點之 ParentId 預設值 (預設為 null)</param>
    /// <returns>組裝完成之階層樹狀結構清單</returns>
    public static List<TNode> BuildTree<TKey, TNode>(
        this IEnumerable<TNode>? items,
        TKey? rootParentId = default)
        where TKey : struct
        where TNode : class, ITreeNode<TKey, TNode>
    {
        if (items == null)
        {
            return new List<TNode>();
        }

        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return new List<TNode>();
        }

        //建立 ParentId -> Children 映射索引 (ToLookup 時間複雜度 O(N))
        var lookup = itemList.ToLookup(x => x.ParentId);

        // 遞迴組裝子樹 (查找雜湊表耗時 O(1))
        List<TNode> BuildChildren(TKey? parentId)
        {
            var children = lookup[parentId].ToList();
            foreach (var child in children)
            {
                child.Children = BuildChildren(child.Id);
            }
            return children;
        }

        return BuildChildren(rootParentId);
    }

    /// <summary>
    /// 帶有 Context (如 TenantLabId/權限/路徑) 上下文傳播需求的泛型樹狀組裝 - O(N) 時間複雜度
    /// </summary>
    /// <typeparam name="TKey">主鍵與外鍵型態</typeparam>
    /// <typeparam name="TNode">實現 ITreeNode 介面之節點類別</typeparam>
    /// <typeparam name="TContext">上下文資料型態 (如 Guid, string)</typeparam>
    /// <param name="items">來源平舖清單</param>
    /// <param name="contextSelector">計算與傳播 Context 的委派 (參數為: 當前節點, 父節點Context; 回傳值為: 當前節點生效Context)</param>
    /// <param name="rootParentId">根節點之 ParentId 預設值</param>
    /// <param name="initialContext">初始上下文預設值</param>
    /// <returns>組裝完成之階層樹狀結構清單</returns>
    public static List<TNode> BuildTreeWithContext<TKey, TNode, TContext>(
        this IEnumerable<TNode>? items,
        Func<TNode, TContext?, TContext?> contextSelector,
        TKey? rootParentId = default,
        TContext? initialContext = default)
        where TKey : struct
        where TNode : class, ITreeNode<TKey, TNode>
    {
        ArgumentNullException.ThrowIfNull(contextSelector, nameof(contextSelector));

        if (items == null)
        {
            return new List<TNode>();
        }

        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return new List<TNode>();
        }

        var lookup = itemList.ToLookup(x => x.ParentId);

        List<TNode> BuildChildren(TKey? parentId, TContext? parentContext)
        {
            var children = lookup[parentId].ToList();
            foreach (var child in children)
            {
                // 計算當前節點之上下文並往下傳遞
                var currentContext = contextSelector(child, parentContext);
                child.Children = BuildChildren(child.Id, currentContext);
            }
            return children;
        }

        return BuildChildren(rootParentId, initialContext);
    }
}