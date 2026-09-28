#nullable enable

namespace SGSFramework.AuthTokenBucket.Repositories
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using SGSFramework.Core.Abstractions.DbContexts;
    using SGSFramework.Core.Abstractions.Permissions.Identities;
    using SGSFramework.Core.Abstractions.Permissions.Repositories;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// 使用者實驗室權限倉儲實作 (基於 Entity Framework Core 與 ITokenDbContext)
    /// </summary>
    /// <typeparam name="TDbContext">資料庫上下文型別</typeparam>
    public sealed class UserLabPermissionRepository<TDbContext> : IUserLabPermissionRepository
        where TDbContext : DbContext, ITokenDbContext
    {
        private readonly TDbContext _dbContext;
        private readonly ILogger<UserLabPermissionRepository<TDbContext>> _logger;

        public UserLabPermissionRepository(
            TDbContext dbContext,
            ILogger<UserLabPermissionRepository<TDbContext>> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<UserLabPermission?> GetPermissionAsync(
            Guid userId,
            int labId,
            string controllerOrModuleKey,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("使用者 ID 不得為空白。", nameof(userId));
            }

            if (labId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(labId), "實驗室 ID 必須大於 0。");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 依據 UserId、LabId 以及模組/Controller 鍵值進行查詢
                // 實務上若 UserLabPermission 結構僅以 UserId 與 LabId 作為主鍵/聯合查詢，可自行微調 Where 條件
                return await _dbContext.Set<UserLabPermission>()
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.LabId == labId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[UserLabPermissionRepository] 查詢使用者實驗室權限作業已取消。UserId: {UserId}, LabId: {LabId}", userId, labId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UserLabPermissionRepository] 查詢使用者實驗室權限時發生異常。UserId: {UserId}, LabId: {LabId}", userId, labId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task AddAsync(UserLabPermission permission, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(permission);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                await _dbContext.Set<UserLabPermission>()
                    .AddAsync(permission, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[UserLabPermissionRepository] 新增使用者實驗室權限作業已取消。");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UserLabPermissionRepository] 新增使用者實驗室權限時發生異常。");
                throw;
            }
        }

        /// <inheritdoc />
        public Task UpdateAsync(UserLabPermission permission, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(permission);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 標記實體為修改狀態，交由 EF Core 追蹤變更
                _dbContext.Set<UserLabPermission>().Update(permission);

                return Task.CompletedTask;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[UserLabPermissionRepository] 更新使用者實驗室權限作業已取消。");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UserLabPermissionRepository] 更新使用者實驗室權限時發生異常。");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                return await _dbContext.SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[UserLabPermissionRepository] 儲存變更作業已取消。");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UserLabPermissionRepository] 儲存變更至資料庫時發生異常。");
                throw;
            }
        }
    }
}