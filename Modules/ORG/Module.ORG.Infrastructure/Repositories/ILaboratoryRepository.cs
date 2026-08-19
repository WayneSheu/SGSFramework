using SGS.Modules.ORG.Infrastructure.Entities.Org;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Infrastructure.Repositories
{
    /// <summary>
    /// 實驗室資料存取介面，提供 CRUD 與樹狀結構查詢功能
    /// </summary>
    public interface ILaboratoryRepository
    {
        Task<Organization?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Organization?> GetTreeByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<Organization>> GetAllAsync(CancellationToken cancellationToken = default);
        Task AddAsync(Organization laboratory, CancellationToken cancellationToken = default);
        void Update(Organization laboratory);
        void Delete(Organization laboratory);
        Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
