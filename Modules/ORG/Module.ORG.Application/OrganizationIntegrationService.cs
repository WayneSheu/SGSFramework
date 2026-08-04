using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Helpers;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Persistent.Repositories.Hierarchy;
using SGS.Modules.ORG.Application.Services;


namespace SGS.Modules.ORG.Application
{
    /// <summary>
    /// 跨模組組織資料整合服務 (防腐層適配器 Adapter)
    /// 負責將 ORG 模組內部的 int 主鍵與 NodePath 邏輯，轉化為 Auth 等外部模組可理解的輕量契約 DTO
    /// </summary>
    public class OrganizationIntegrationService : IOrganizationIntegrationService
    {
        private readonly IOrganizationService _orgService;
        private readonly IHierarchicalRepository<ORGDbContext, Organization> _orgRepo;
        private readonly ORGDbContext _dbContext;

        public OrganizationIntegrationService(
            IOrganizationService orgService,
            IHierarchicalRepository<ORGDbContext, Organization> orgRepo,
            ORGDbContext dbContext)
        {
            _orgService = orgService ?? throw new ArgumentNullException(nameof(orgService));
            _orgRepo = orgRepo ?? throw new ArgumentNullException(nameof(orgRepo));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// 獲取單一組織節點資訊 (提供跨模組 DTO)
        /// </summary>
        /// <param name="orgId">內部 int ID 對映之 Deterministic Guid</param>
        /// <param name="cancellationToken">取消權杖</param>
        public async Task<OrganizationInfoContract?> GetOrganizationByIdAsync(
            Guid orgId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _orgRepo.GetByIdAsync(orgId.ToInternalIntId());
                if (entity is null || entity.IsDeleted) 
                {
                    return null;
                }

                // 使用 Helper 擴充方法將內部 int ID 一致性地轉換為跨模組 Guid
                Guid mappedGuid = entity.Id.ToDeterministicGuid();

                return new OrganizationInfoContract(
                    mappedGuid,
                    entity.Name,
                    entity.NodePath,
                    entity.Level
                );
            }
            catch (Exception)
            {
                // 依據防禦性原則，發生異常時回傳 null
                return null;
            }
        }

        /// <summary>
        /// 驗證目標組織 (targetOrgId) 是否在使用者 (userId) 的管轄/存取組織階層範疇內
        /// </summary>
        /// <param name="userId">使用者識別碼</param>
        /// <param name="targetOrgId">跨模組 Guid 格式的目標組織 ID</param>
        /// <param name="cancellationToken">取消權杖</param>
        public async Task<bool> IsInUserScopeAsync(
            string userId,
            Guid targetOrgId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            try
            {
                // 1. 將跨模組傳入的確定性 Guid 還原為內部的 int ID[cite: 3]
                int internalTargetOrgId = targetOrgId.ToInternalIntId();

                // 2. 查詢目標組織節點，取得其 NodePath (例如: "/1/5/10/")
                var targetNode = await _orgRepo.GetByIdAsync(internalTargetOrgId);
                if (targetNode is null || targetNode.IsDeleted) 
                {
                    return false;
                }

                // 3. 獲取該使用者被賦予的管轄組織節點 NodePath 清單
                List<string> userScopeNodePaths = await GetUserAssignedScopeNodePathsAsync(userId, cancellationToken);
                if (userScopeNodePaths is null || !userScopeNodePaths.Any())
                {
                    return false;
                }

                // 4. 執行 NodePath 樹狀範疇比對 (.StartsWith)
                string targetPath = targetNode.NodePath; 

                bool isMatch = userScopeNodePaths.Any(userPath =>
                    !string.IsNullOrEmpty(userPath) && targetPath.StartsWith(userPath, StringComparison.OrdinalIgnoreCase));

                return isMatch;
            }
            catch (Exception)
            {
                // 發生異常時預設拒絕存取
                return false;
            }
        }

        /// <summary>
        /// 獲取使用者有權存取的所有組織節點清單
        /// </summary>
        /// <param name="userId">使用者識別碼</param>
        /// <param name="cancellationToken">取消權杖</param>
        public async Task<List<OrganizationInfoContract>> GetUserAccessibleOrganizationsAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<OrganizationInfoContract>();
            }

            try
            {
                // 1. 取得使用者具備管轄權限的 NodePath 清單
                List<string> userScopeNodePaths = await GetUserAssignedScopeNodePathsAsync(userId, cancellationToken);
                if (!userScopeNodePaths.Any())
                {
                    return new List<OrganizationInfoContract>();
                }

                // 2. 從資料庫中查出所有非軟刪除的組織節點
                var allNodes = await _dbContext.Set<Organization>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .ToListAsync(cancellationToken);

                // 3. 過濾出屬於使用者管轄範疇 (NodePath 前綴匹配) 的節點
                var accessibleNodes = allNodes
                    .Where(node => userScopeNodePaths.Any(scopePath =>
                        !string.IsNullOrEmpty(scopePath) && node.NodePath.StartsWith(scopePath, StringComparison.OrdinalIgnoreCase)))
                    .Select(entity => new OrganizationInfoContract(
                        entity.Id.ToDeterministicGuid(),
                        entity.Name,
                        entity.NodePath,
                        entity.Level
                    ))
                    .ToList();

                return accessibleNodes;
            }
            catch (Exception)
            {
                return new List<OrganizationInfoContract>();
            }
        }

        #region Private Helpers

        /// <summary>
        /// 查詢使用者被直接授權或管轄的組織 NodePath 列表
        /// </summary>
        private async Task<List<string>> GetUserAssignedScopeNodePathsAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            // 實務上在此處查詢 UserOrganizationScopes 關聯表或 User 權限設定
            // 範例：回傳主管管轄的路徑 (如 Root "/1/" 表示管轄底下的所有實驗室與部門)
            return await Task.FromResult(new List<string> { "/1/", "/1/5/" });
        }

        #endregion
    }
}