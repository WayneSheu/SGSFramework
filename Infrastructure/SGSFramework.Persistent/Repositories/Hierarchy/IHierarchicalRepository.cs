using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Hierarchical;

namespace SGSFramework.Persistent.Repositories.Hierarchy;

/// <summary>
/// 支援多 DbContext 動態切換之階層結構倉儲介面 (符合 Clean Architecture 與企業級異步規範)
/// </summary>
/// <typeparam name="TDbContext">DbContext 類型</typeparam>
/// <typeparam name="TEntity">實作 IHierarchicalEntity 之階層領域實體</typeparam>
public interface IHierarchicalRepository<TDbContext, TEntity>
    where TDbContext : DbContext
    where TEntity : class, IHierarchicalEntity
{
    /// <summary>
    /// 依據主鍵 ID 取得單一節點實體
    /// </summary>
    Task<TEntity?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// 取得所有根節點 (ParentId 為 null)
    /// </summary>
    IQueryable<TEntity> GetRoots();

    /// <summary>
    /// 取得指定節點路徑下的所有子孫節點 (物化路徑匹配)
    /// </summary>
    IQueryable<TEntity> GetDescendants(string nodePath);

    /// <summary>
    /// 取得指定父節點下的直接子節點
    /// </summary>
    IQueryable<TEntity> GetDirectChildren(int parentId);

    /// <summary>
    /// 新增階層節點 (可傳入已載入的父實體以減少 DB 額外查詢)
    /// </summary>
    Task<TEntity> AddNodeAsync(TEntity entity, TEntity? parentNode, CancellationToken ct = default);

    /// <summary>
    /// 新增階層節點 (自動依據 parentId 尋找父節點)
    /// </summary>
    Task<TEntity> AddNodeAsync(TEntity entity, int? parentId, CancellationToken ct = default);

    /// <summary>
    /// 覆蓋編輯節點
    /// </summary>
    Task<TEntity> EditNodeAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>
    /// 部分更新節點 (僅針對非 null 屬性更新，保護 NodePath 與 Level 不被覆蓋)
    /// </summary>
    Task<TEntity> PatchNodeAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>
    /// 移動完整子樹：更新目標節點及其所有子孫節點之 ParentId、NodePath 與 Level
    /// </summary>
    Task<bool> MoveSubtreeAsync(int nodeId, int? newParentId, CancellationToken ct = default);

    /// <summary>
    /// 刪除節點及其所有子孫 (支援實體軟刪除 ISoftDeletable 自動判定)
    /// </summary>
    Task<bool> DeleteNodeWithChildrenAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// 儲存變更
    /// </summary>
    Task<bool> SaveChangesAsync(CancellationToken ct = default);
}