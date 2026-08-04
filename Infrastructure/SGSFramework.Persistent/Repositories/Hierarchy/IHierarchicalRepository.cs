using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Hierarchical;

namespace SGSFramework.Persistent.Repositories.Hierarchy
{
    /// <summary>
    /// 支援多 DbContext 動態切換之階層結構倉儲介面
    /// </summary>
    public interface IHierarchicalRepository<TDbContext, TEntity>
        where TDbContext : DbContext
        where TEntity : class, IHierarchicalEntity
    {
        Task<TEntity> GetByIdAsync(int id);
        IQueryable<TEntity> GetRoots();
        IQueryable<TEntity> GetDescendants(string nodePath);
        IQueryable<TEntity> GetDirectChildren(int parentId);

        Task<TEntity> PatchNodeAsync(TEntity entity, CancellationToken ct = default);
        Task<TEntity> EditNodeAsync(TEntity entity, CancellationToken ct = default);

        Task<TEntity> AddNodeAsync(TEntity entity, int? parentId, CancellationToken ct = default);
        Task<bool> MoveSubtreeAsync(int nodeId, int? newParentId, CancellationToken ct = default);
        Task<bool> DeleteNodeWithChildrenAsync(int nodeId, CancellationToken ct = default);
        Task<bool> SaveChangesAsync(CancellationToken ct = default);
    }
}
