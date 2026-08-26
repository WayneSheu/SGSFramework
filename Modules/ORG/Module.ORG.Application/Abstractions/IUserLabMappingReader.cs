using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Abstractions
{

    /// <summary>
    /// 定義解耦讀取合約
    /// </summary>
    public interface IUserLabMappingReader
    {
        /// <summary>
        /// 檢查使用者是否為系統管理員 (Sysadmin)
        /// </summary>
        Task<bool> IsSysadminAsync(Guid userId, CancellationToken cancellationToken);

        /// <summary>
        /// 取得使用者直屬的實驗室對應清單
        /// </summary>
        Task<List<UserLabMappingDto>> GetAccessibleMappingsAsync(Guid userId, CancellationToken cancellationToken);
    }
}
