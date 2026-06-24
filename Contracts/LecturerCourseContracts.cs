using Microsoft.AspNetCore.Http;

namespace LMS.Api.Contracts;

public record LecturerCourseOfferingDto(
    Guid Id,
    Guid CourseId,
    string CourseCode,
    string CourseTitle,
    int CreditUnits,
    Guid ProgramId,
    string ProgramName,
    Guid LevelId,
    string LevelName,
    Guid AcademicSessionId,
    string AcademicSessionName,
    int Semester,
    int EnrolledStudentCount,
    int UpcomingSessionsCount);

public record LecturerCoursesResponse(
    List<LecturerCourseOfferingDto> Courses,
    int TotalCourses,
    int TotalStudents);

// Course Detail Contracts
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
    Guid ProgramId,
    string ProgramName,
    Guid LevelId,
    string LevelName,
    Guid AcademicSessionId,
    string AcademicSessionName,
    int Semester,
    List<CourseMaterialDto> Materials,
    List<CourseStudentDto> Students,
    int MaterialsCount,
    int StudentsCount);

public record AddCourseMaterialRequest(
    string Title,
    string? Description,
    IFormFile File);

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
