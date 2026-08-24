using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Adapters
{
    /// <summary>
    /// 使用者與實驗室關聯資料存取介面
    /// </summary>
    public interface IUserLabRepository
    {
        /// <summary>
        /// 取得使用者的主要實驗室
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<UserLabMapping?> GetPrimaryLabAsync(Guid userId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 取得使用者可訪問的所有實驗室列表
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<List<UserLabMapping>> GetAccessibleLabsAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 設定使用者的主要實驗室
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="newPrimaryLabId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task SetPrimaryLabAsync(Guid userId, int newPrimaryLabId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 新增或更新使用者的次要實驗室關聯
        /// </summary>
        /// <param name="mapping"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task AddOrUpdateSecondaryLabAsync(UserLabMapping mapping, CancellationToken cancellationToken = default);

    }
}
