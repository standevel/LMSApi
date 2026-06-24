using ErrorOr;
using LMS.Api.Contracts;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Services;

public interface IProgramSwitchService
{
    /// <summary>Student submits a new program switch request.</summary>
    Task<ErrorOr<ProgramSwitchRequestDto>> CreateRequestAsync(
        Guid studentId, CreateProgramSwitchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Student uploads the required JAMB admission letter.
    /// Moves status from Draft → PendingHoDReview.
    /// </summary>
    Task<ErrorOr<ProgramSwitchRequestDto>> UploadJambDocumentAsync(
        Guid requestId, Guid studentId, IFormFile file, CancellationToken ct = default);

    /// <summary>
    /// HoD approves or rejects the request.
    /// Requires status = PendingHoDReview AND JAMB document present.
    /// Approve → PendingDeanReview. Reject → RejectedByHoD.
    /// </summary>
    Task<ErrorOr<ProgramSwitchRequestDto>> HoDReviewAsync(
        Guid requestId, Guid reviewerId, bool approved, string? notes, string? rejectionReason, CancellationToken ct = default);

    /// <summary>
    /// Dean approves or rejects the request.
    /// Requires status = PendingDeanReview (enforces HoD approval occurred first) AND JAMB document present.
    /// Approve → PendingAdminAction. Reject → RejectedByDean.
    /// </summary>
    Task<ErrorOr<ProgramSwitchRequestDto>> DeanReviewAsync(
        Guid requestId, Guid reviewerId, bool approved, string? notes, string? rejectionReason, CancellationToken ct = default);

    /// <summary>
    /// Admin/Registrar completes the program switch.
    /// Requires status = PendingAdminAction (enforces HoD + Dean approved) AND JAMB document present.
    /// Updates Student entity, ProgramEnrollment, and triggers new DegreeAudit evaluation.
    /// </summary>
    Task<ErrorOr<ProgramSwitchRequestDto>> AdminCompleteAsync(
        Guid requestId, Guid adminId, string? notes, CancellationToken ct = default);

    /// <summary>
    /// Admin/Registrar rejects the request at their stage.
    /// </summary>
    Task<ErrorOr<ProgramSwitchRequestDto>> AdminRejectAsync(
        Guid requestId, Guid adminId, string rejectionReason, CancellationToken ct = default);

    /// <summary>Get all switch requests submitted by a specific student.</summary>
    Task<ErrorOr<List<ProgramSwitchRequestSummaryDto>>> GetStudentRequestsAsync(
        Guid studentId, CancellationToken ct = default);

    /// <summary>
    /// Get requests pending for a specific role queue.
    /// role: "HoD" → PendingHoDReview; "Dean" → PendingDeanReview; "Admin" → PendingAdminAction
    /// </summary>
    Task<ErrorOr<List<ProgramSwitchRequestSummaryDto>>> GetPendingForRoleAsync(
        string role, CancellationToken ct = default);

    /// <summary>Get all switch requests (Admin overview with optional status filter).</summary>
    Task<ErrorOr<List<ProgramSwitchRequestSummaryDto>>> GetAllRequestsAsync(
        string? statusFilter, CancellationToken ct = default);

    /// <summary>Get full detail of a specific switch request.</summary>
    Task<ErrorOr<ProgramSwitchRequestDto>> GetByIdAsync(
        Guid requestId, CancellationToken ct = default);
}
