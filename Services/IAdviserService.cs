using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IAdviserService
{
    Task<ErrorOr<List<AdviserUserDto>>> GetEligibleAdvisersAsync(Guid actorId, Guid? departmentId, Guid? facultyId, CancellationToken ct = default);
    Task<ErrorOr<AdviserAssignmentDto>> AssignAdviserAsync(Guid actorId, AssignAdviserRequest request, CancellationToken ct = default);
    Task<ErrorOr<List<AdviserAssignmentDto>>> BulkAssignAdviserAsync(Guid actorId, BulkAssignAdviserRequest request, CancellationToken ct = default);
    Task<ErrorOr<AutoAssignAdvisersResultDto>> AutoAssignAdvisersAsync(Guid actorId, AutoAssignAdvisersRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> EndAssignmentAsync(Guid actorId, Guid assignmentId, CancellationToken ct = default);
    Task<ErrorOr<AdviserDashboardDto>> GetDashboardAsync(Guid actorId, CancellationToken ct = default);
    Task<ErrorOr<List<AdviserStudentSummaryDto>>> GetAssignedStudentsAsync(Guid actorId, CancellationToken ct = default);
    Task<ErrorOr<AdvisingStudentProfileDto>> GetStudentProfileAsync(Guid actorId, Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<List<AdvisingNoteDto>>> GetNotesAsync(Guid actorId, Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<AdvisingNoteDto>> CreateNoteAsync(Guid actorId, Guid studentId, CreateAdvisingNoteRequest request, CancellationToken ct = default);
    Task<ErrorOr<RegistrationVerificationDto>> VerifyRegistrationAsync(Guid actorId, Guid studentId, VerifyRegistrationRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> UnlockRegistrationAsync(Guid actorId, Guid studentId, UnlockRegistrationVerificationRequest request, CancellationToken ct = default);
    Task<bool> IsRegistrationLockedAsync(Guid studentId, Guid academicSessionId, CancellationToken ct = default);
}
