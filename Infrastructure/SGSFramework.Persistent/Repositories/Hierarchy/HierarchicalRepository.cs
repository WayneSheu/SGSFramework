using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Hierarchical;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using System.Reflection;

namespace SGSFramework.Persistent.Repositories.Hierarchy;

/// <summary>
/// 企業級高效能樹狀/階層實體 Repository 基礎實作
/// </summary>
/// <typeparam name="TDbContext">DbContext 型態</typeparam>
/// <typeparam name="TEntity">實作 IHierarchicalEntity 之領域實體</typeparam>
public class HierarchicalRepository<TDbContext, TEntity> : IHierarchicalRepository<TDbContext, TEntity>
    where TDbContext : DbContext
    where TEntity : class, IHierarchicalEntity
{
    protected readonly TDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    public HierarchicalRepository(TDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = context.Set<TEntity>();
    }

    /// <summary>
    /// 根據主鍵 Id 取得單一節點實體 (支援 CancellationTokens)
    /// </summary>
    public async Task<TEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(n => n.Id == id, ct);

    /// <summary>
    /// 取得所有根節點 (ParentId 為 null)
    /// </summary>
    public IQueryable<TEntity> GetRoots() => _dbSet.Where(n => n.ParentId == null);

    /// <summary>
    /// 取得指定節點路徑下的所有子孫節點 (物化路徑匹配)
    /// </summary>
    public IQueryable<TEntity> GetDescendants(string nodePath)
        => _dbSet.Where(n => n.NodePath.StartsWith(nodePath) && n.NodePath != nodePath);

    /// <summary>
    /// 取得指定父節點下的直接子節點
    /// </summary>
    public IQueryable<TEntity> GetDirectChildren(int parentId)
        => _dbSet.Where(n => n.ParentId == parentId);

    /// <summary>
    /// 新增階層節點並自動維護 NodePath 與 Level
    /// </summary>
    public async Task<TEntity> AddNodeAsync(TEntity entity, TEntity? parentNode, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(entity));

        var entry = _context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            await _dbSet.AddAsync(entity, ct);
        }

        // 1. 首次存檔取得資料庫自動遞增之 Id
        await _context.SaveChangesAsync(ct);

        // 2. 使用 EF Property 設定 NodePath 與 Level (繞過介面唯讀限制)
        var newPath = parentNode != null ? $"{parentNode.NodePath}{entity.Id}/" : $"/{entity.Id}/";
        var newLevel = parentNode != null ? parentNode.Level + 1 : 1;

        entry.Property(nameof(IHierarchicalEntity.NodePath)).CurrentValue = newPath;
        entry.Property(nameof(IHierarchicalEntity.Level)).CurrentValue = newLevel;

        // 3. 更新物化路徑資訊
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>
    /// 新增階層節點 (自動尋找父節點)
    /// </summary>
    public async Task<TEntity> AddNodeAsync(TEntity entity, int? parentId, CancellationToken ct = default)
    {
        TEntity? parent = parentId.HasValue ? await GetByIdAsync(parentId.Value, ct) : null;
        return await AddNodeAsync(entity, parent, ct);
    }

    /// <summary>
    /// 覆蓋編輯節點：將傳入實體的值完全覆蓋目前被追蹤的實體
    /// </summary>
    public async Task<TEntity> EditNodeAsync(TEntity entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(entity));

        try
        {
            var trackedNode = await GetByIdAsync(entity.Id, ct)
                ?? throw new InvalidOperationException($"找不到主鍵為 {entity.Id} 的節點，無法進行編輯。");

            var entry = _context.Entry(trackedNode);

            // 備份並鎖定關鍵階層欄位，避免覆蓋時破壞物化路徑結構
            var currentPath = trackedNode.NodePath;
            var currentLevel = trackedNode.Level;
            var currentParentId = trackedNode.ParentId;

            // 覆蓋所有值
            entry.CurrentValues.SetValues(entity);

            // 還原階層結構屬性（移動結構需透過 MoveSubtreeAsync 處理）
            entry.Property(nameof(IHierarchicalEntity.NodePath)).CurrentValue = currentPath;
            entry.Property(nameof(IHierarchicalEntity.Level)).CurrentValue = currentLevel;
            entry.Property(nameof(IHierarchicalEntity.ParentId)).CurrentValue = currentParentId;

            await _context.SaveChangesAsync(ct);
            return trackedNode;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"資料庫更新失敗，請檢查條件約束。詳細原因: {ex.InnerException?.Message ?? ex.Message}", ex);
        }
    }

    /// <summary>
    /// 高效能部分更新 (Patch)：基於 EF Core 屬性與元資料進行，避免動態反射開銷
    /// </summary>
    public async Task<TEntity> PatchNodeAsync(TEntity entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(entity));

        var trackedNode = await GetByIdAsync(entity.Id, ct)
            ?? throw new InvalidOperationException($"找不到 ID 為 {entity.Id} 的節點，無法進行部分更新。");

        var entry = _context.Entry(trackedNode);
        var entityType = entry.Metadata;

        foreach (var property in entityType.GetProperties())
        {
            // 跳過主鍵或關鍵階層結構控制欄位，防止錯誤覆蓋
            if (property.IsPrimaryKey() ||
                property.Name == nameof(IHierarchicalEntity.NodePath) ||
                property.Name == nameof(IHierarchicalEntity.Level))
            {
                continue;
            }

            var newValue = property.PropertyInfo?.GetValue(entity);

            // 只有傳入非 null 的新值才進行覆蓋更新
            if (newValue is not null)
            {
                var propertyEntry = entry.Property(property.Name);
                if (!Equals(propertyEntry.CurrentValue, newValue))
                {
                    propertyEntry.CurrentValue = newValue;
                    propertyEntry.IsModified = true;
                }
            }
        }

        if (entry.State == EntityState.Modified)
        {
            await _context.SaveChangesAsync(ct);
        }

        return trackedNode;
    }

    /// <summary>
    /// 移動完整子樹：更新目標節點及其所有子孫節點的 ParentId, NodePath 與 Level
    /// </summary>
    public async Task<bool> MoveSubtreeAsync(int nodeId, int? newParentId, CancellationToken ct = default)
    {
        var targetNode = await GetByIdAsync(nodeId, ct);
        if (targetNode is null) return false;

        TEntity? newParent = newParentId.HasValue ? await GetByIdAsync(newParentId.Value, ct) : null;

        var oldPathPrefix = targetNode.NodePath;
        var oldLevel = targetNode.Level;

        var newPathPrefix = newParent != null ? $"{newParent.NodePath}{targetNode.Id}/" : $"/{targetNode.Id}/";
        var newLevel = newParent != null ? newParent.Level + 1 : 1;
        var levelOffset = newLevel - oldLevel;

        // 1. 透過 EF Property 更新目標節點自身
        var targetEntry = _context.Entry(targetNode);
        targetEntry.Property(nameof(IHierarchicalEntity.ParentId)).CurrentValue = newParentId;
        targetEntry.Property(nameof(IHierarchicalEntity.NodePath)).CurrentValue = newPathPrefix;
        targetEntry.Property(nameof(IHierarchicalEntity.Level)).CurrentValue = newLevel;

        // 2. 一次性查出所有子孫節點進行批量路徑與階層校正
        var descendants = await GetDescendants(oldPathPrefix).ToListAsync(ct);
        foreach (var descendant in descendants)
        {
            var descendantEntry = _context.Entry(descendant);
            var updatedPath = newPathPrefix + descendant.NodePath[oldPathPrefix.Length..];
            var updatedLevel = descendant.Level + levelOffset;

            descendantEntry.Property(nameof(IHierarchicalEntity.NodePath)).CurrentValue = updatedPath;
            descendantEntry.Property(nameof(IHierarchicalEntity.Level)).CurrentValue = updatedLevel;
        }

        return await _context.SaveChangesAsync(ct) > 0;
    }

    /// <summary>
    /// 刪除節點及其子孫：自動判定並支援硬刪除或軟刪除 (Soft Delete)
    /// </summary>
    public async Task<bool> DeleteNodeWithChildrenAsync(int nodeId, CancellationToken ct = default)
    {
        var targetNode = await GetByIdAsync(nodeId, ct);
        if (targetNode is null) return false;

        var descendants = await GetDescendants(targetNode.NodePath).ToListAsync(ct);
        var allNodesToDelete = new List<TEntity> { targetNode }.Concat(descendants);

        // 檢查是否實作 ISoftDeletable 介面
        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            foreach (var node in allNodesToDelete)
            {
                if (node is ISoftDeletable softDeletableNode)
                {
                    softDeletableNode.IsDeleted = true;
                    softDeletableNode.DeletedOnUtc = DateTimeOffset.UtcNow;
                }
            }
        }
        else
        {
            _dbSet.RemoveRange(allNodesToDelete);
        }

        return await _context.SaveChangesAsync(ct) > 0;
    }

    public async Task<bool> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct) > 0;
}