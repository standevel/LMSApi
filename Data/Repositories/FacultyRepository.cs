using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Repositories;

public sealed class FacultyRepository(LmsDbContext dbContext)
    : BaseRepository<Faculty>(dbContext), IFacultyRepository
{
}
