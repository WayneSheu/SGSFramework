using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Persistence
{
    public interface IQueryRepository<T> where T : class
    {
        IQueryable<T> GetQueryable();
        Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    }
}
