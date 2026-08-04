using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.Hierarchical;
using System.Reflection;

namespace SGSFramework.Persistent.Repositories.Hierarchy
{
    public class HierarchicalRepository<TDbContext, TEntity> : IHierarchicalRepository<TDbContext, TEntity>
    where TDbContext : DbContext
    where TEntity : class, IHierarchicalEntity
    {
        protected readonly TDbContext _context;
        protected readonly DbSet<TEntity> _dbSet;

        public HierarchicalRepository(TDbContext context)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
        }

        /// <summary>
        /// 根據主鍵 Id 取得單一節點實體。此方法會從資料庫中查詢具有指定 Id 的節點，並回傳對應的 TEntity 實體。
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>

        public async Task<TEntity> GetByIdAsync(int id) => await _dbSet.FirstOrDefaultAsync(n => n.Id == id);



        /// <summary>
        /// 取得所有根節點（ParentId 為 null 的節點）。根節點是階層結構的頂層元素，沒有父節點。
        /// </summary>
        /// <returns></returns>
        public IQueryable<TEntity> GetRoots() => _dbSet.Where(n => n.ParentId == null);

        /// <summary>
        /// 取得指定節點路徑下的所有子孫節點（NodePath 以 nodePath 開頭且不等於 nodePath 的節點）。這些節點是指定節點的直接或間接子節點。
        /// </summary>
        /// <param name="nodePath"></param>
        /// <returns></returns>
        public IQueryable<TEntity> GetDescendants(string nodePath)
            => _dbSet.Where(n => n.NodePath.StartsWith(nodePath) && n.NodePath != nodePath);

        /// <summary>
        /// 取得指定父節點下的直接子節點（ParentId 等於 parentId 的節點）。這些節點是指定父節點的直接子節點，不包含更深層的子孫節點。
        /// </summary>
        /// <param name="parentId"></param>
        /// <returns></returns>
        public IQueryable<TEntity> GetDirectChildren(int parentId)
            => _dbSet.Where(n => n.ParentId == parentId);

        /// <summary>
        /// 新增階層節點，設定 ParentId、Level 與 NodePath，並將變更儲存後回傳該實體。
        /// </summary>
        /// <remarks>先新增實體以產生 Id，接著更新 NodePath 為 "/{id}/" 或以父節點路徑拼接為
        /// "{parent.NodePath}{id}/"，並再次儲存以更新路徑。</remarks>
        /// <param name="entity">要新增並儲存的節點實體。</param>
        /// <param name="parentId">父節點的 Id；為 null 表示建立為根節點。</param>
        /// <param name="ct">可用於取消非同步操作的 CancellationToken（選用）。</param>
        /// <returns>包含更新後 Id 與 NodePath 的已儲存實體。</returns>

        public async Task<TEntity> AddNodeAsync(TEntity entity, int? parentId, CancellationToken ct = default)
        {
            // 1. 取得父節點路徑 (確保資料已從 DB 載入)
            var parent = parentId.HasValue ? await _dbSet.FindAsync(new object[] { parentId }, ct) : null;

            // 2. 加入實體到 Context，此時狀態為 Added
            entity.NodePath = entity.NodePath ?? string.Empty;
            await _dbSet.AddAsync(entity, ct);

            // 3. 在存檔前，先執行一次 SaveChanges 僅為了取得 Id (這是唯一一次無法避免的寫入)
            // 但為了讓 AuditInterceptor 知道這是一個整體的「新增」操作，我們建議調整邏輯
            await _context.SaveChangesAsync(ct);

            // 4. 更新 NodePath
            entity.NodePath = parent != null ? $"{parent.NodePath}{entity.Id}/" : $"/{entity.Id}/";

            // 5. 若此時 Update 該實體，Interceptor 會視為 Modified
            // 為了避免 Interceptor 混亂，請確保 Interceptor 支援忽略某些屬性的變更
            await _context.SaveChangesAsync(ct);

            return entity;
        }

        /// <summary>
        /// 將新值覆蓋到目前被追蹤的實體上（此時 entity 內部的 null 會覆蓋原本的正確值）
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<TEntity> EditNodeAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "傳入的實體不可為 Null。");
            }

            try
            {
                // 取得資料庫目前的最新狀態並追蹤
                TEntity? trackedNode = await _dbSet.FirstOrDefaultAsync(n => n.Id == entity.Id, ct);

                if (trackedNode == null)
                {
                    throw new InvalidOperationException($"找不到主鍵為 {entity.Id} 的節點，無法進行更新。");
                }
                // 使用 EF Core 的 Entry API 來覆蓋值，這樣可以確保只有實際變更的欄位才會被標記為 Modified
                var entry = _context.Entry(trackedNode);

                // 將新值覆蓋到目前被追蹤的實體上（此時 entity 內部的 null 會覆蓋原本的正確值）
                entry.CurrentValues.SetValues(entity);
                await _context.SaveChangesAsync(ct);

                return trackedNode;
            }
            catch (DbUpdateException ex)
            {
                // 擷取內部 SqlException 資訊以利除錯，並包裝丟出
                throw new InvalidOperationException($"資料庫更新失敗，請檢查條件約束。詳細原因: {ex.InnerException?.Message ?? ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("執行節點編輯時發生未預期的錯誤。", ex);
            }
        }

        /// <summary>
        /// 部分更新節點：只更新傳入實體中非 null 的屬性，並確保不會覆蓋 NodePath、Level 等關鍵欄位。
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<TEntity> PatchNodeAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "傳入的更新實體不可為 Null。");
            }

            try
            {
                // 1. 取得資料庫目前的最新狀態並追蹤
                TEntity? trackedNode = await _dbSet.FirstOrDefaultAsync(n => n.Id == entity.Id, ct);

                if (trackedNode == null)
                {
                    throw new InvalidOperationException($"找不到主鍵為 {entity.Id} 的節點，無法進行部分更新。");
                }

                var entry = _context.Entry(trackedNode);

                // 2. 利用反射動態比對屬性，實現「有值才覆蓋」
                PropertyInfo[] properties = typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var prop in properties)
                {
                    // 跳過主鍵 Id，不進行更新比對
                    if (prop.Name == nameof(IEntity<long>.Id))
                    {
                        continue;
                    }

                    // 取得新傳入物件的值
                    object? newValue = prop.GetValue(entity);

                    // 【核心邏輯】只有當新值不為 null 時才進行覆蓋
                    // 如果你的值型態（如 int）預設值為 0 代表不更新，可在此加入額外條件： && !newValue.Equals(0)
                    if (newValue != null)
                    {
                        var efProperty = entry.Property(prop.Name);
                        if (efProperty != null)
                        {
                            // 取得資料庫當前的值
                            object? currentValue = efProperty.CurrentValue;

                            // 如果新舊值不相等，才寫入並標記為已修改
                            if (!Equals(currentValue, newValue))
                            {
                                efProperty.CurrentValue = newValue;
                                efProperty.IsModified = true;
                            }
                        }
                    }
                }

                // 3. 只有當有欄位被實際變更時，才向資料庫發出 UPDATE SQL
                if (entry.State == EntityState.Modified)
                {
                    await _context.SaveChangesAsync(ct);
                }

                return trackedNode;
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException($"部分更新資料庫失敗。詳細原因: {ex.InnerException?.Message ?? ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("執行節點部分更新時發生未預期的錯誤。", ex);
            }
        }

        /// <summary>
        /// 移動子樹：將指定節點移動到新的父節點下，並更新該節點及其所有子孫的 NodePath 和 Level。
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="newParentId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<bool> MoveSubtreeAsync(int nodeId, int? newParentId, CancellationToken ct = default)
        {
            var node = await _dbSet.FindAsync(new object[] { nodeId }, ct);
            if (node == null) return false;

            var oldPath = node.NodePath;
            var newParent = newParentId.HasValue ? await _dbSet.FindAsync(new object[] { newParentId }, ct) : null;
            string newPathPrefix = newParent?.NodePath ?? "/";

            node.ParentId = newParentId;
            node.NodePath = $"{newPathPrefix}{node.Id}/";
            node.Level = (newParent?.Level ?? 0) + 1;

            // 批次更新所有子孫路徑
            var descendants = await GetDescendants(oldPath).ToListAsync(ct);
            foreach (var d in descendants)
            {
                d.NodePath = d.NodePath.Replace(oldPath, node.NodePath);
                d.Level = node.Level + (d.NodePath.Split('/').Length - node.NodePath.Split('/').Length);
            }

            return await _context.SaveChangesAsync(ct) > 0;
        }

        /// <summary>
        /// 刪除節點及其子孫：刪除指定節點以及所有子孫節點，確保資料完整性。
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<bool> DeleteNodeWithChildrenAsync(int nodeId, CancellationToken ct = default)
        {
            var node = await _dbSet.FindAsync(new object[] { nodeId }, ct);
            if (node == null) return false;

            var descendants = await GetDescendants(node.NodePath).ToListAsync(ct);

            _dbSet.Remove(node);
            _dbSet.RemoveRange(descendants);

            return await _context.SaveChangesAsync(ct) > 0;
        }

        public async Task<bool> SaveChangesAsync(CancellationToken ct = default)
            => await _context.SaveChangesAsync(ct) > 0;
    }
}
