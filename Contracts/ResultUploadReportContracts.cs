using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

public sealed class ResultUploadAggregatesRequest
{
    public Guid? AcademicSessionId { get; set; }
    public int? Semester { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? ProgramId { get; set; }
    public string? Status { get; set; } // all, uploaded, pending, in_approval, published
    public string? SearchTerm { get; set; }
}

public sealed record ResultUploadSessionDto(
    Guid Id,
    string Name,
    bool IsActive,
    int ActiveSemester);

public sealed record ResultUploadOverallStatsDto(
    int TotalOfferings,
    int UploadedOfferings,
    double UploadedPercentage,
    int PendingOfferings,
    double PendingPercentage,
    int InApprovalOfferings,
    int PublishedOfferings,
    double PublishedPercentage,
    int TotalEnrolledStudents,          // Headcount of distinct students (< 300)
    int TotalCourseRegistrations,       // Total course registrations across all offerings (e.g. 1639)
    int TotalGradedStudents,            // Total graded course registrations
    int DistinctGradedStudents,         // Headcount of distinct students with grades
    double GradingCompletionPercentage);

public sealed record CollegeResultSummaryDto(
    Guid CollegeId,
    string CollegeName,
    string CollegeCode,
    int TotalOfferings,
    int UploadedOfferings,
    int PendingOfferings,
    int InApprovalOfferings,
    int PublishedOfferings,
    double UploadPercentage,
    int TotalEnrolledStudents,          // Distinct students in this college
    int TotalCourseRegistrations,       // Total registrations in this college
    int TotalGradedStudents,            // Graded registrations in this college
    double GradingPercentage);

public sealed record ProgramResultSummaryDto(
    Guid ProgramId,
    string ProgramName,
    string ProgramCode,
    Guid DepartmentId,
    string DepartmentName,
    Guid CollegeId,
    string CollegeName,
    int TotalOfferings,
    int UploadedOfferings,
    int PendingOfferings,
    int InApprovalOfferings,
    int PublishedOfferings,
    double UploadPercentage,
    int TotalEnrolledStudents,          // Distinct students in this program
    int TotalCourseRegistrations,       // Total registrations in this program
    int TotalGradedStudents);

public sealed record CourseOfferingResultDetailDto(
    Guid OfferingId,
    Guid CourseId,
    string CourseCode,
    string CourseTitle,
    int CreditUnits,
    Guid? CollegeId,
    string CollegeName,
    Guid? DepartmentId,
    string DepartmentName,
    Guid? ProgramId,
    string ProgramName,
    List<Guid> ProgramIds,              // All program IDs associated with this course offering
    string LevelName,
    int Semester,
    List<string> LecturerNames,
    int EnrolledCount,
    int GradedCount,
    double CompletionRate,
    string Status, // Pending, Uploaded, InApproval, Approved, Published
    DateTime? LastUploadDate,
    string? UploadedBy,
    DateTime? PublishedAt,
    bool IsPublished,
    bool HasAssessments);

public sealed record ResultUploadAggregatesResponse(
    ResultUploadSessionDto Session,
    int Semester,
    ResultUploadOverallStatsDto OverallStats,
    List<CollegeResultSummaryDto> Colleges,
    List<ProgramResultSummaryDto> Programs,
    List<CourseOfferingResultDetailDto> Courses);
