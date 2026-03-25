using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Data.Repositories;

public interface IBaseRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(TEntity entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
