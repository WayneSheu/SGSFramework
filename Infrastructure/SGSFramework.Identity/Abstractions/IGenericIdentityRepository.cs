using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Abstractions
{
    /// <summary>
    /// 泛型化 Identity 倉儲服務介面
    /// </summary>
    /// <typeparam name="TUser">使用者實體型別</typeparam>
    /// <typeparam name="TKey">主鍵型別</typeparam>
    public interface IGenericIdentityRepository<TUser, TKey>
        where TUser : IdentityUser<TKey>
        where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// 透過 Email 取得使用者
        /// </summary>
        Task<TUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新使用者登入時間
        /// </summary>
        Task<bool> UpdateLastLoginAsync(TKey userId, CancellationToken cancellationToken = default);
    }
}
