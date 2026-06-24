using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IRegistrationService
{
    Task<ErrorOr<CourseRegistrationDto>> RegisterStudent(Guid studentId, Guid courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DropCourse(Guid studentId, Guid enrollmentId, CancellationToken ct = default);
    Task<ErrorOr<RegistrationSummaryDto>> GetRegistrationSummaryAsync(Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<RegistrationSummaryDto>> RegisterCoursesBulk(Guid studentId, List<Guid> courseOfferingIds, CancellationToken ct = default);
    Task<ErrorOr<CourseSwapRequestDto>> RequestCourseSwapAsync(Guid studentId, Guid currentCourseOfferingId, Guid newCourseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<CourseSwapOptionsDto>> GetCourseSwapOptionsAsync(Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<List<CourseSwapRequestDto>>> GetSwapRequestsAsync(Guid? studentId = null, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> ProcessSwapRequestAsync(Guid requestId, bool approved, string? adminNotes, CancellationToken ct = default);
    Task<ErrorOr<List<CourseRegistrationDto>>> GetRegistrationHistoryAsync(Guid studentId, CancellationToken ct = default);
}
