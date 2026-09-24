namespace LMS.Api.Contracts;

public record RegistrationCleanupScanRequest(
    Guid? AcademicSessionId = null,
    Guid? ProgramId = null,
    Guid? LevelId = null,
    bool CheckProgramMismatch = true,
    bool CheckLevelMismatch = true,
    bool AllowPotentialCarryover = true,
    bool CheckAlreadyPassed = true);

public record InvalidRegistrationItemDto(
    Guid EnrollmentId,
    Guid StudentId,
    string StudentName,
    string MatricNumber,
    Guid? ProgramId,
    string? ProgramName,
    Guid? LevelId,
    string? LevelName,
    int? StudentLevelOrder,
    Guid CourseOfferingId,
    Guid CourseId,
    string CourseCode,
    string CourseTitle,
    int Semester,
    string? CourseLevelName,
    int? CourseLevelOrder,
    List<string> Issues,
    DateTime RegisteredAtUtc);

public record RegistrationCleanupScanResultDto(
    Guid AcademicSessionId,
    string AcademicSessionName,
    int TotalEnrollmentsScanned,
    int TotalInvalidFound,
    int ProgramMismatchCount,
    int LevelMismatchCount,
    int AlreadyPassedCount,
    List<InvalidRegistrationItemDto> InvalidRegistrations);

public record ExecuteRegistrationCleanupRequest(
    Guid? AcademicSessionId = null,
    Guid? ProgramId = null,
    Guid? LevelId = null,
    bool CheckProgramMismatch = true,
    bool CheckLevelMismatch = true,
    bool AllowPotentialCarryover = true,
    bool CheckAlreadyPassed = true,
    List<Guid>? SpecificEnrollmentIds = null,
    string? Reason = null);

public record RegistrationCleanupExecutionResultDto(
    int TotalCleanedUp,
    string Reason,
    DateTime ExecutedAtUtc,
    List<CleanedUpRegistrationDto> CleanedRegistrations);
