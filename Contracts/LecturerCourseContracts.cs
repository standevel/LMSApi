using Microsoft.AspNetCore.Http;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

// ─── Lecturer My-Courses ──────────────────────────────────────────────────────

public record LecturerCourseOfferingDto(
    Guid Id,
    Guid CourseId,
    string CourseCode,
    string CourseTitle,
    int CreditUnits,
    /// <summary>Combined comma-separated program names for display.</summary>
    string ProgramNames,
    /// <summary>Combined comma-separated level names for display.</summary>
    string LevelNames,
    Guid AcademicSessionId,
    string AcademicSessionName,
    int Semester,
    CourseLecturerRole Role,
    int EnrolledStudentCount,
    int UpcomingSessionsCount,
    bool IsPublished = false);

public record LecturerCoursesResponse(
    List<LecturerCourseOfferingDto> Courses,
    int TotalCourses,
    int TotalStudents);

// ─── Course Detail (Lecturer-facing) ─────────────────────────────────────────

public record CourseMaterialDto(
    Guid Id,
    string Title,
    string? Description,
    string FileUrl,
    string? FileType,
    long? FileSize,
    DateTime UploadedAt,
    string UploadedByName);

public record CourseStudentDto(
    Guid Id,
    string StudentNumber,
    string FullName,
    string Email,
    DateTime EnrolledAt,
    string? Grade);

public record CourseDetailResponse(
    Guid Id,
    string CourseCode,
    string CourseTitle,
    string? Description,
    int CreditUnits,
    /// <summary>All programs + levels attached to this offering.</summary>
    List<OfferingProgramDto> Programs,
    Guid AcademicSessionId,
    string AcademicSessionName,
    int Semester,
    List<OfferingLecturerDto> Lecturers,
    List<CourseMaterialDto> Materials,
    List<CourseStudentDto> Students,
    int MaterialsCount,
    int StudentsCount);

public record AddCourseMaterialRequest(
    string Title,
    string? Description,
    IFormFile? File,
    string? LinkUrl);

public record AddCourseMaterialResponse(
    Guid Id,
    string Title,
    string FileUrl,
    DateTime UploadedAt);

// ─── Student-facing course detail DTOs ───────────────────────────────────────

/// <summary>Score breakdown for the student's own grade in this course.</summary>
public record StudentCourseGradeDto(
    double? Ca1Score,
    double? Ca2Score,
    double? Ca3Score,
    double? ExamScore,
    double? TotalScore,
    string? LetterGrade,
    double? GradePoints,
    bool IsPublished);

/// <summary>Bucket histogram for score distribution across the class + student position.</summary>
public record CourseClassAnalyticsDto(
    double ClassAverage,
    double? StudentScore,
    int? StudentPercentile,
    int TotalStudentsWithGrades,
    IReadOnlyList<ScoreBucketDto> Buckets);

/// <summary>One 10-point bucket of the score histogram (e.g. 60–70).</summary>
public record ScoreBucketDto(
    int RangeStart,
    int RangeEnd,
    int Count);

/// <summary>Student-facing course detail: info + materials + personal grade + class analytics.</summary>
public record StudentCourseDetailResponse(
    Guid Id,
    string CourseCode,
    string CourseTitle,
    string? Description,
    int CreditUnits,
    string ProgramName,
    string LevelName,
    string AcademicSessionName,
    int Semester,
    List<CourseMaterialDto> Materials,
    int MaterialsCount,
    StudentCourseGradeDto? Grade,
    CourseClassAnalyticsDto? Analytics);
