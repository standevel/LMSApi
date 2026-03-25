using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Repositories;

public interface ICurriculumRepository : IBaseRepository<Curriculum>
{
    Task<List<Curriculum>> GetByProgramIdAsync(Guid programId, CancellationToken ct = default);
    Task<CurriculumCourse?> GetCourseAsync(Guid curriculumId, Guid courseId, CancellationToken ct = default);
    Task AddCourseAsync(CurriculumCourse curriculumCourse, CancellationToken ct = default);
    Task UpdateCourseAsync(CurriculumCourse curriculumCourse, CancellationToken ct = default);
    Task DeleteCourseAsync(CurriculumCourse curriculumCourse, CancellationToken ct = default);
    Task<ProgramEnrollment?> GetEnrollmentAsync(Guid studentId, Guid sessionId, CancellationToken ct = default);
    Task AddEnrollmentAsync(ProgramEnrollment enrollment, CancellationToken ct = default);
    Task<List<CoursePrerequisite>> GetPrerequisitesAsync(Guid courseId, CancellationToken ct = default);
    Task AddPrerequisiteAsync(CoursePrerequisite prerequisite, CancellationToken ct = default);
    Task DeletePrerequisiteAsync(Guid id, CancellationToken ct = default);
    Task<List<AuditLog>> GetAuditLogsAsync(string entityName, string entityId, CancellationToken ct = default);
}
