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
    Task<ErrorOr<Deleted>> DropCourse(Guid enrollmentId, CancellationToken ct = default);
    Task<ErrorOr<CourseSwapRequestDto>> RequestCourseSwapAsync(Guid studentId, Guid currentCourseOfferingId, Guid newCourseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<List<CourseSwapRequestDto>>> GetSwapRequestsAsync(CancellationToken ct = default);
    Task<ErrorOr<Deleted>> ProcessSwapRequestAsync(Guid requestId, bool approved, string? adminNotes, CancellationToken ct = default);
    Task<ErrorOr<List<ProgramEnrollmentDto>>> GetRegistrationHistoryAsync(Guid studentId, CancellationToken ct = default);
}