using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Repositories;

public interface IRoleRepository
{
    Task<AppRole?> GetByNameAsync(string roleName, CancellationToken ct = default);
}
