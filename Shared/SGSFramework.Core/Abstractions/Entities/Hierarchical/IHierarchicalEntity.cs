using Microsoft.AspNetCore.Hosting.Server;

namespace SGSFramework.Core.Abstractions.Entities.Hierarchical
{
    /// <summary>
    /// 樹狀階層實體統一介面
    /// **符合企業級 DDD 與 Clean Architecture 規範**
    /// ## 介面設計優點分析
    /// ** 唯讀屬性契約(`{ get; }`)**：
    /// **介面僅暴露 Getter，不強制子類別提供 Setter，這完美支援了領域實體（Domain Entity）將 Setter 設為 `private` 或 `protected` 的需求。
    /// ** 高效能主鍵(`int`)**：
    /// ** 在階層樹狀結構中，選用 `int` 配合 SQL Server 的 Clustered Index 可最大化 B-Tree 索引效能，顯著降低 `NodePath` 字串運算與 Join 雜湊負擔。
    /// ** 物化路徑(Materialized Path) 支援**：
    /// ** 透過 `NodePath`（如 `/1/5/10/`）能將傳統樹狀結構遞迴查詢轉譯為高效率的 `LIKE 'Path%'` 查詢，極大地提升了廣度/深度搜尋效能。
    /// </summary>
    public interface IHierarchicalEntity
    {
        /// <summary>
        /// 實體唯一識別碼 (使用 int 確保資料庫 Clustered Index 索引效能)
        /// </summary>
        int Id { get; }

        /// <summary>
        /// 父節點識別碼 (頂層根節點為 null)
        /// </summary>
        int? ParentId { get; }

        /// <summary>
        /// 樹狀物化路徑 (例如: "/1/5/10/")，用於高效進行子樹與階層查詢
        /// </summary>
        string NodePath { get; }

        /// <summary>
        /// 節點所處階層深度 (0: 類別目錄, 1: 實體頂層節點, 2+: 內部子單位)
        /// </summary>
        int Level { get; }
    }
}
