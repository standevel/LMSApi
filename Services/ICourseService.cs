using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface ICourseService
{
    Task<ErrorOr<CourseDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<CourseDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ErrorOr<CourseDto>> CreateAsync(CreateCourseRequest request, CancellationToken ct = default);
    Task<ErrorOr<CourseDto>> UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<CourseDto>> ToggleStatusAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<SimpleUserDto>>> GetLecturersAsync(CancellationToken ct = default);
    Task<ErrorOr<LecturerCoursesResponse>> GetMyCoursesAsync(Guid lecturerId, CancellationToken ct = default);
    
    // Course Detail Methods
    Task<ErrorOr<CourseDetailResponse>> GetCourseDetailAsync(Guid offeringId, Guid lecturerId, CancellationToken ct = default);
    Task<ErrorOr<AddCourseMaterialResponse>> AddCourseMaterialAsync(Guid offeringId, Guid lecturerId, AddCourseMaterialRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteCourseMaterialAsync(Guid materialId, Guid lecturerId, CancellationToken ct = default);
}
