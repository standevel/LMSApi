using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class CourseEquivalencyService(LmsDbContext dbContext) : ICourseEquivalencyService
{
    public async Task<IEnumerable<CourseEquivalency>> GetEquivalenciesAsync(
        string sourceInstitution,
        string sourceCourseCode,
        CancellationToken ct = default)
    {
        return await dbContext.CourseEquivalencies
            .Where(e => e.SourceInstitution == sourceInstitution
                && e.SourceCourseCode == sourceCourseCode
                && e.IsActive)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<CourseEquivalency>> GetInstitutionEquivalenciesAsync(
        string sourceInstitution,
        CancellationToken ct = default)
    {
        return await dbContext.CourseEquivalencies
            .Where(e => e.SourceInstitution == sourceInstitution && e.IsActive)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<CourseEquivalency>> GetAllActiveEquivalenciesAsync(
        Guid? targetCourseId = null,
        CancellationToken ct = default)
    {
        var query = dbContext.CourseEquivalencies
            .Where(e => e.IsActive)
            .AsQueryable();

        if (targetCourseId.HasValue)
        {
            query = query.Where(e => e.TargetCourseId == targetCourseId);
        }

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }
}
