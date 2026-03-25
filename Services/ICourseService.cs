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
}
