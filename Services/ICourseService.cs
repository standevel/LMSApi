using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface ICourseService
{
    // ─── Course CRUD ───────────────────────────────────────────────────────────
    Task<ErrorOr<CourseDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<CourseDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ErrorOr<CourseDto>> CreateAsync(CreateCourseRequest request, CancellationToken ct = default);
    Task<ErrorOr<CourseDto>> UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<CourseDto>> ToggleStatusAsync(Guid id, CancellationToken ct = default);

    // ─── Lecturers list ────────────────────────────────────────────────────────
    Task<ErrorOr<List<SimpleUserDto>>> GetLecturersAsync(CancellationToken ct = default);

    // ─── My Courses ────────────────────────────────────────────────────────────
    Task<ErrorOr<LecturerCoursesResponse>> GetMyCoursesAsync(
        Guid lecturerId, bool isAdmin = false,
        Guid? academicSessionId = null, CancellationToken ct = default);

    // ─── Offerings list (admin) ────────────────────────────────────────────────
    Task<ErrorOr<List<CourseOfferingDto>>> GetCourseOfferingsAsync(
        Guid? academicSessionId = null, CancellationToken ct = default);

    // ─── Program attachment ────────────────────────────────────────────────────
    Task<ErrorOr<CourseOfferingDto>> AttachProgramAsync(
        Guid offeringId, Guid programId, Guid levelId, CancellationToken ct = default);

    Task<ErrorOr<CourseOfferingDto>> DetachProgramAsync(
        Guid offeringId, Guid programId, Guid levelId, CancellationToken ct = default);

    // ─── Lecturer assignment ───────────────────────────────────────────────────
    Task<ErrorOr<CourseOfferingDto>> AssignLecturerAsync(
        Guid offeringId, Guid? lecturerId,
        List<Guid>? coLecturerIds, CancellationToken ct = default);

    Task<ErrorOr<BulkAssignLecturersResult>> BulkAssignLecturersAsync(
        List<OfferingAssignment> assignments, CancellationToken ct = default);

    // ─── Course Detail (Lecturer-facing) ───────────────────────────────────────
    Task<ErrorOr<CourseDetailResponse>> GetCourseDetailAsync(
        Guid offeringId, Guid lecturerId, CancellationToken ct = default);

    Task<ErrorOr<AddCourseMaterialResponse>> AddCourseMaterialAsync(
        Guid offeringId, Guid lecturerId,
        AddCourseMaterialRequest request, CancellationToken ct = default);

    Task<ErrorOr<Deleted>> DeleteCourseMaterialAsync(
        Guid materialId, Guid lecturerId, CancellationToken ct = default);

    // ─── Student-facing ────────────────────────────────────────────────────────
    Task<ErrorOr<StudentCourseDetailResponse>> GetStudentCourseDetailAsync(
        Guid offeringId, Guid studentId, CancellationToken ct = default);
}
