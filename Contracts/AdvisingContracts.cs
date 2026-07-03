namespace LMS.Api.Contracts;

public record AdviserUserDto(
    Guid Id,
    string DisplayName,
    string? Email,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? FacultyId,
    string? FacultyName,
    int ActiveAdviseeCount);

public record AdviserAssignmentDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string? StudentNumber,
    Guid AdviserId,
    string AdviserName,
    string Status,
    string Source,
    string? Note,
    DateTime AssignedAtUtc);

public record AssignAdviserRequest(Guid StudentId, Guid AdviserId, string? Note);

public record BulkAssignAdviserRequest(IReadOnlyList<Guid> StudentIds, Guid AdviserId, string? Note);

public record AutoAssignAdvisersRequest(Guid? DepartmentId, Guid? FacultyId);

public record AutoAssignAdvisersResultDto(int AssignedCount, int SkippedCount, IReadOnlyList<AdviserAssignmentDto> Assignments);

public record AdviserDashboardDto(
    int AssignedStudents,
    int VerifiedRegistrations,
    int UnverifiedRegistrations,
    int FollowUpsDue,
    IReadOnlyList<AdviserStudentSummaryDto> RecentStudents);

public record AdviserStudentSummaryDto(
    Guid StudentId,
    string StudentName,
    string? StudentNumber,
    string? ProgramName,
    string? DepartmentName,
    string? FacultyName,
    string? LevelName,
    bool RegistrationVerified,
    DateTime? VerifiedAtUtc,
    string? AdviserName);

public record AdvisingNoteDto(
    Guid Id,
    Guid StudentId,
    Guid AdviserId,
    string AdviserName,
    string Title,
    string Body,
    DateTime? FollowUpDateUtc,
    bool IsStaffOnly,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record CreateAdvisingNoteRequest(string Title, string Body, DateTime? FollowUpDateUtc);

public record RegistrationVerificationDto(
    Guid Id,
    Guid StudentId,
    Guid AcademicSessionId,
    string AcademicSessionName,
    Guid VerifiedByAdviserId,
    string VerifiedByAdviserName,
    DateTime VerifiedAtUtc,
    string Status,
    string? Remarks);

public record VerifyRegistrationRequest(string? Remarks);

public record UnlockRegistrationVerificationRequest(string Reason);

public record AdvisingStudentProfileDto(
    AdviserStudentSummaryDto Student,
    RegistrationSummaryDto RegistrationSummary,
    IReadOnlyList<CourseSwapRequestDto> SwapRequests,
    IReadOnlyList<AdvisingNoteDto> Notes,
    RegistrationVerificationDto? RegistrationVerification);
