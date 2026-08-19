using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.TreeNodes
{
    /// <summary>
    /// 樹狀節點介面，實現此介面之 DTO 或 Model 可自動支援泛型樹狀組裝
    /// </summary>
    /// <typeparam name="TKey">鍵值型態 (例如 int, Guid, string)</typeparam>
    /// <typeparam name="TNode">節點自身型態</typeparam>
    public interface ITreeNode<TKey, TNode>
        where TKey : struct
        where TNode : class, ITreeNode<TKey, TNode>
    {
        TKey Id { get; }
        TKey? ParentId { get; }
        List<TNode> Children { get; set; }
    }
}
