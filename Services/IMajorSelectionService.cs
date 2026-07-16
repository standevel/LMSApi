using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IMajorSelectionService
{
    /// <summary>
    /// Gets available child specializations for the student's current program.
    /// </summary>
    Task<ErrorOr<List<SpecializationOptionDto>>> GetAvailableSpecializationsAsync(
        Guid studentId, CancellationToken ct = default);

    /// <summary>
    /// Student submits a request to declare a major/specialization.
    /// </summary>
    Task<ErrorOr<MajorDeclarationRequestDto>> CreateDeclarationRequestAsync(
        Guid studentId, CreateMajorDeclarationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Get pending major declaration requests for an Adviser's students.
    /// </summary>
    Task<ErrorOr<List<MajorDeclarationRequestDto>>> GetPendingRequestsForAdviserAsync(
        Guid adviserId, CancellationToken ct = default);

    /// <summary>
    /// Get all major declaration requests for a student.
    /// </summary>
    Task<ErrorOr<List<MajorDeclarationRequestDto>>> GetStudentRequestsAsync(
        Guid studentId, CancellationToken ct = default);

    /// <summary>
    /// Adviser reviews and approves or rejects a declaration request.
    /// On approval, executes the program switch internally.
    /// </summary>
    Task<ErrorOr<MajorDeclarationRequestDto>> ReviewRequestAsync(
        Guid requestId, Guid adviserId, ReviewMajorDeclarationRequest review, CancellationToken ct = default);

    /// <summary>
    /// Gets a major declaration request by ID.
    /// </summary>
    Task<ErrorOr<MajorDeclarationRequestDto>> GetByIdAsync(
        Guid requestId, CancellationToken ct = default);
}
