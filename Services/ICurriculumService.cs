using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface ICurriculumService
{
    Task<ErrorOr<CurriculumDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<CurriculumSummaryDto>>> GetByProgramIdAsync(Guid programId, CancellationToken ct = default);
    Task<ErrorOr<CurriculumDto>> CreateCurriculumAsync(Guid programId, CreateCurriculumRequest request, CancellationToken ct = default);
    Task<ErrorOr<CurriculumDto>> AddCourseAsync(Guid curriculumId, AddCurriculumCourseRequest request, CancellationToken ct = default);
    Task<ErrorOr<CurriculumDto>> UpdateCourseAsync(Guid curriculumId, Guid id, UpdateCurriculumCourseRequest request, CancellationToken ct = default);
    Task<ErrorOr<CurriculumDto>> AddCoursesBulkAsync(Guid curriculumId, BulkAddCurriculumCourseRequest request, CancellationToken ct = default);
    Task<ErrorOr<EnrollmentDto>> EnrollStudentAsync(EnrollStudentRequest request, CancellationToken ct = default);
    Task<ErrorOr<CurriculumDto>> CloneCurriculumAsync(Guid curriculumId, string newName, CancellationToken ct = default);
    Task<ErrorOr<CurriculumDto>> PublishCurriculumAsync(Guid curriculumId, CancellationToken ct = default);
    Task<ErrorOr<bool>> ValidatePrerequisitesAsync(Guid curriculumId, CancellationToken ct = default);
    Task<ErrorOr<bool>> AddPrerequisiteAsync(Guid courseId, AddCoursePrerequisiteRequest request, CancellationToken ct = default);
    Task<ErrorOr<List<CurriculumHistoryDto>>> GetHistoryAsync(Guid curriculumId, CancellationToken ct = default);
}
