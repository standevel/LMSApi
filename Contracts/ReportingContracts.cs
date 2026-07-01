using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

// ==================== GPA ====================

public record GpaDto(
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    decimal CumulativeGpa,
    int TotalCreditsAttempted,
    int TotalCreditsEarned,
    decimal CreditsEarnedGpa,
    string AcademicSessionName,
    string StandingType,
    DateTime CalculatedAt);

public record SessionGpaDto(
    Guid StudentId,
    string AcademicSessionName,
    decimal SessionGpa,
    int SessionCreditsAttempted,
    int SessionCreditsEarned,
    DateTime SessionDate);

// ==================== TRANSCRIPT ====================

public record TranscriptDto(
    Guid StudentId,
    string StudentName,
    string StudentNumber,
    string Email,
    string ProgramName,
    string LevelName,
    ProgramType ProgramType,
    DateTime DateOfBirth,
    string Nationality,
    string AdmissionSessionName,
    List<TranscriptCourseRecord> CourseRecords,
    decimal CumulativeGpa,
    int TotalCreditsEarned,
    string StandingType,
    bool IsOfficial,
    string? GeneratedBy,
    DateTime GeneratedAt);

public record TranscriptCourseRecord(
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    int CreditUnits,
    int Semester,
    string AcademicSessionName,
    string? GradeLetter,
    decimal? GradePoints,
    int AttendancePercentage);

public record CreateTranscriptRequestDto(
    Guid? StudentId,
    bool IsOfficial,
    string? DeliveryEmail,
    string? DeliveryMethod,
    string? Remarks);

public record TranscriptRequestDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    bool IsOfficial,
    TranscriptStatus Status,
    string? DeliveryEmail,
    string DeliveryMethod,
    decimal? FeeAmount,
    bool FeePaid,
    string? DocumentUrl,
    string? ProcessedBy,
    DateTime CreatedAt,
    DateTime? CompletedAt);

// ==================== DEGREE AUDIT ====================

public record DegreeAuditDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid ProgramId,
    string ProgramName,
    DegreeAuditStatus Status,
    int TotalCreditsRequired,
    int TotalCreditsEarned,
    int TotalCreditsInProgress,
    decimal CumulativeGpa,
    string? Summary,
    List<DegreeAuditRequirementDto> Requirements,
    DateTime GeneratedAt,
    DateTime? CompletedAt);

public record DegreeAuditRequirementDto(
    Guid Id,
    string CategoryName,
    RequirementCategory Category,
    string? RequirementName,
    int CreditsRequired,
    int CreditsEarned,
    bool IsCompleted,
    string? Remarks);

public record CreateDegreeAuditRequest(
    Guid StudentId,
    Guid ProgramId,
    Guid? TemplateId);

// ==================== DEGREE REQUIREMENT ====================

public record DegreeRequirementDto(
    Guid Id,
    Guid ProgramId,
    string ProgramName,
    string Name,
    RequirementType Type,
    int CreditHoursRequired,
    decimal MinGpaRequired,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    List<DegreeRequirementCourseDto> Courses);

public record DegreeRequirementCourseDto(
    Guid Id,
    Guid CourseId,
    string CourseCode,
    string CourseTitle,
    bool IsRequired,
    int MinGrade,
    string? Remarks);

public record CreateDegreeRequirementRequest(
    Guid ProgramId,
    string Name,
    RequirementType Type,
    int CreditHoursRequired,
    decimal MinGpaRequired,
    string? Description,
    int DisplayOrder,
    List<CreateDegreeRequirementCourseRequest>? Courses);

public record CreateDegreeRequirementCourseRequest(
    Guid CourseId,
    bool IsRequired,
    int MinGrade,
    string? Remarks);

public record UpdateDegreeRequirementRequest(
    string Name,
    RequirementType Type,
    int CreditHoursRequired,
    decimal MinGpaRequired,
    string? Description,
    int DisplayOrder,
    bool IsActive);

// ==================== ANALYTICS ====================

public record EnrollmentAnalyticsDto(
    int TotalEnrollments,
    int NewEnrollments,
    int ReturningEnrollments,
    int DroppedEnrollments,
    int ActiveEnrollments,
    List<EnrollmentByProgramDto> EnrollmentsByProgram,
    List<EnrollmentByFacultyDto> EnrollmentsByFaculty,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public record EnrollmentByProgramDto(
    Guid ProgramId,
    string ProgramName,
    int EnrollmentCount);

public record EnrollmentByFacultyDto(
    Guid FacultyId,
    string FacultyName,
    int EnrollmentCount);

public record GraduationRatesDto(
    List<GraduationRateDto> GraduationRates,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public record GraduationRateDto(
    string ProgramName,
    int TotalGraduates,
    int ExpectedGraduates,
    decimal GraduationRate,
    string AcademicSessionName);

// ==================== DASHBOARD ====================

public record DashboardSummaryDto(
    int TotalStudents,
    int TotalLecturers,
    int TotalCourseOfferings,
    int ActiveAcademicSessions,
    int TotalPrograms,
    int PendingTranscriptRequests,
    StudentGpaOverview StudentGpaOverview,
    EnrollmentTrendDto EnrollmentTrend,
    List<DepartmentStatsDto> DepartmentStats);

public record StudentGpaOverview(
    decimal AverageGpa,
    int StudentsOnProbation,
    int StudentsOnDeanList,
    int TotalStudentsWithGpa);

public record EnrollmentTrendDto(
    List<MonthlyEnrollmentDto> MonthlyEnrollments,
    int CurrentTotal,
    int PreviousTotal,
    decimal GrowthPercentage);

public record MonthlyEnrollmentDto(
    int Month,
    string MonthName,
    int EnrollmentCount);

public record DepartmentStatsDto(
    Guid DepartmentId,
    string DepartmentName,
    string FacultyName,
    int ProgramCount,
    int StudentCount,
    int LecturerCount);

public record FacultyDashboardDto(
    Guid FacultyId,
    string FacultyName,
    string Label,
    int TotalDepartments,
    int TotalPrograms,
    int TotalStudents,
    int TotalLecturers,
    List<DepartmentSummaryDto> Departments);

public record DepartmentSummaryDto(
    Guid DepartmentId,
    string DepartmentName,
    string DepartmentCode,
    int ProgramCount,
    int StudentCount,
    int LecturerCount);

public record DepartmentDashboardDto(
    Guid DepartmentId,
    string DepartmentName,
    string FacultyName,
    int TotalPrograms,
    int TotalStudents,
    int TotalLecturers,
    List<ProgramStatsDto> Programs,
    List<LecturerStatsDto> Lecturers);

public record ProgramStatsDto(
    Guid ProgramId,
    string ProgramName,
    string ProgramCode,
    int StudentCount,
    int ActiveOfferings,
    decimal AverageGpa);

public record LecturerStatsDto(
    Guid LecturerId,
    string LecturerName,
    string Email,
    int CourseCount,
    int TotalStudents,
    decimal AverageAttendance);
