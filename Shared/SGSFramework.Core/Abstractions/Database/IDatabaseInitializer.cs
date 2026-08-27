using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Database
{
    /// <summary>
    /// 資料庫初始化服務介面
    /// </summary>
    public interface IDatabaseInitializer
    {
        /// <summary>
        /// 執行資料庫自動化初始化流程
        /// </summary>
        /// <param name="cancellationToken">取消標記</param>
        Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);

    }
}
