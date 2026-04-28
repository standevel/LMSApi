using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Repositories;

public sealed class DepartmentRepository(LmsDbContext dbContext)
    : BaseRepository<Department>(dbContext), IDepartmentRepository
{
}
