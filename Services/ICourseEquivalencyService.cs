using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface ICourseEquivalencyService
{
    /// <summary>
    /// Gets course equivalencies for a source institution and course code.
    /// </summary>
    Task<IEnumerable<CourseEquivalency>> GetEquivalenciesAsync(
        string sourceInstitution,
        string sourceCourseCode,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all active equivalencies for a source institution.
    /// </summary>
    Task<IEnumerable<CourseEquivalency>> GetInstitutionEquivalenciesAsync(
        string sourceInstitution,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all active equivalencies optionally filtered by target course.
    /// </summary>
    Task<IEnumerable<CourseEquivalency>> GetAllActiveEquivalenciesAsync(
        Guid? targetCourseId = null,
        CancellationToken ct = default);
}
