using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.Identity.Abstractions
{
    /// <summary>
    /// Identity 倉儲服務介面
    /// </summary>
    public interface IIdentityRepository
    {
        /// <summary>
        /// 透過 Email 取得使用者
        /// </summary>
        Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新使用者登入時間
        /// </summary>
        Task<bool> UpdateLastLoginAsync(Guid userId, CancellationToken cancellationToken = default);

    }
}
